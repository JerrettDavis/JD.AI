using System.Text;
using JD.AI.Tui.Rendering;
using JD.AI.Tui.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Tui.Agent;

/// <summary>Type of subagent with scoped tool access and model preference.</summary>
public enum SubagentType
{
    /// <summary>Fast codebase analysis — read-only tools, cheap model.</summary>
    Explore,

    /// <summary>Run commands, report pass/fail — shell + read tools, cheap model.</summary>
    Task,

    /// <summary>Create implementation plans — read + search + memory, smart model.</summary>
    Plan,

    /// <summary>Code review on diffs/files — read + git + search, smart model.</summary>
    Review,

    /// <summary>Full capability — all tools, same model as parent.</summary>
    General,
}

/// <summary>
/// Manages subagent lifecycle: creates isolated Kernel instances with scoped tools,
/// runs a single turn, and returns the result string.
/// </summary>
public sealed class SubagentRunner
{
    private readonly AgentSession _parentSession;

    public SubagentRunner(AgentSession parentSession)
    {
        _parentSession = parentSession;
    }

    /// <summary>
    /// Spawns a subagent of the given type, sends it a prompt, and returns its response.
    /// The subagent runs in the same process but with its own Kernel, ChatHistory, and scoped tools.
    /// </summary>
    public async Task<string> RunAsync(
        SubagentType type,
        string prompt,
        CancellationToken ct = default)
    {
        ChatRenderer.RenderInfo($"  🔀 Spawning {type} subagent...");

        var kernel = BuildScopedKernel(type);
        var history = new ChatHistory();

        history.AddSystemMessage(GetSystemPrompt(type));
        history.AddUserMessage(prompt);

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096,
        };

        try
        {
            var fullResponse = new StringBuilder();
            await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                history, settings, kernel, ct).ConfigureAwait(false))
            {
                if (chunk.Content is { Length: > 0 } text)
                {
                    fullResponse.Append(text);
                }
            }

            var result = fullResponse.Length > 0 ? fullResponse.ToString() : "(no response)";
            ChatRenderer.RenderInfo($"  ✅ {type} subagent complete ({result.Length} chars)");
            return result;
        }
        catch (OperationCanceledException)
        {
            return $"[{type} subagent cancelled]";
        }
#pragma warning disable CA1031 // non-fatal subagent failure
        catch (Exception ex)
        {
            ChatRenderer.RenderWarning($"  ⚠ {type} subagent failed: {ex.Message}");
            return $"[{type} subagent error: {ex.Message}]";
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Spawns multiple subagents in parallel and returns all results.
    /// </summary>
    public async Task<IDictionary<string, string>> RunParallelAsync(
        IEnumerable<(SubagentType Type, string Label, string Prompt)> tasks,
        CancellationToken ct = default)
    {
        var work = tasks.Select(async t =>
        {
            var result = await RunAsync(t.Type, t.Prompt, ct).ConfigureAwait(false);
            return (t.Label, Result: result);
        });

        var results = await System.Threading.Tasks.Task.WhenAll(work).ConfigureAwait(false);
        return results.ToDictionary(r => r.Label, r => r.Result, StringComparer.Ordinal);
    }

    private Kernel BuildScopedKernel(SubagentType type)
    {
        // Clone the parent kernel's chat completion service
        var parentKernel = _parentSession.Kernel;
        var builder = Kernel.CreateBuilder();

        // Copy the chat completion service from parent
        var chatService = parentKernel.GetRequiredService<IChatCompletionService>();
        builder.Services.AddSingleton(chatService);

        var kernel = builder.Build();

        // Register only the tools appropriate for this subagent type
        var toolSets = GetToolSet(type);
        foreach (var plugin in parentKernel.Plugins)
        {
            if (toolSets.Contains(plugin.Name, StringComparer.OrdinalIgnoreCase))
            {
                kernel.Plugins.Add(plugin);
            }
        }

        return kernel;
    }

    private static HashSet<string> GetToolSet(SubagentType type) => type switch
    {
        SubagentType.Explore => ["FileTools", "SearchTools", "GitTools", "MemoryTools"],
        SubagentType.Task => ["ShellTools", "FileTools", "SearchTools"],
        SubagentType.Plan => ["FileTools", "SearchTools", "MemoryTools", "GitTools"],
        SubagentType.Review => ["FileTools", "SearchTools", "GitTools"],
        SubagentType.General => ["FileTools", "SearchTools", "GitTools", "ShellTools", "WebTools", "MemoryTools"],
        _ => [],
    };

    private static string GetSystemPrompt(SubagentType type) => type switch
    {
        SubagentType.Explore => """
            You are an explore subagent. Your job is to quickly analyze code and answer questions.
            Use search and read tools to find relevant code. Return focused answers under 300 words.
            Do NOT modify any files.
            """,
        SubagentType.Task => """
            You are a task subagent. Your job is to execute commands and report results.
            On success, return a brief summary (e.g., "All 247 tests passed", "Build succeeded").
            On failure, return the full error output (stack traces, compiler errors).
            """,
        SubagentType.Plan => """
            You are a planning subagent. Your job is to create structured implementation plans.
            Analyze the codebase, understand the architecture, and create a step-by-step plan
            with specific files to create/modify, components to build, and testing strategy.
            """,
        SubagentType.Review => """
            You are a code review subagent. Analyze diffs and files for:
            - Bugs and logic errors
            - Security vulnerabilities
            - Performance issues
            Only surface issues that genuinely matter. Never comment on style or formatting.
            """,
        SubagentType.General => """
            You are a general-purpose subagent with full tool access.
            Complete the assigned task thoroughly and report results.
            """,
        _ => "You are a subagent. Complete the assigned task.",
    };
}
