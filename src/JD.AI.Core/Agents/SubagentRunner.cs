using System.Text;
using JD.AI.Core.Agents.Tasks;
using JD.AI.Core.PromptCaching;
using JD.AI.Core.Providers;
using JD.AI.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.Agents;

/// <summary>
/// Manages subagent lifecycle: creates isolated Kernel instances with scoped tools,
/// runs a single turn, and returns the result string.
/// </summary>
public sealed class SubagentRunner
{
    private readonly AgentSession _parentSession;
    private readonly IToolLoadoutRegistry? _loadoutRegistry;
    private readonly IAgentTaskRegistry? _taskRegistry;

    /// <summary>
    /// Initialises a <see cref="SubagentRunner"/> using the parent session's kernel and tools.
    /// </summary>
    /// <param name="parentSession">The owning agent session.</param>
    /// <param name="loadoutRegistry">
    /// Optional loadout registry. When provided, subagent tool sets are resolved via the
    /// registry using the loadout mapped to each <see cref="SubagentType"/>.
    /// </param>
    /// <param name="taskRegistry">
    /// Optional task registry for tracking concurrent subagent tasks.
    /// </param>
    public SubagentRunner(
        AgentSession parentSession,
        IToolLoadoutRegistry? loadoutRegistry = null,
        IAgentTaskRegistry? taskRegistry = null)
    {
        _parentSession = parentSession;
        _loadoutRegistry = loadoutRegistry;
        _taskRegistry = taskRegistry;
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
        AgentOutput.Current.RenderInfo($"  🔀 Spawning {type} subagent...");

        var kernel = BuildScopedKernel(type);
        var history = new ChatHistory();

        history.AddSystemMessage(GetSystemPrompt(type));
        history.AddUserMessage(prompt);

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var supportsTools = _parentSession.CurrentModel?.Capabilities
            .HasFlag(ModelCapabilities.ToolCalling) ?? false;
        var settings = AgentExecutionSettingsFactory.Create(
            _parentSession.CurrentModel,
            supportsTools);
        AgentLoop.ApplyReasoningEffort(
            settings,
            _parentSession.CurrentModel,
            _parentSession.ReasoningEffortOverride);
        PromptCachePolicy.Apply(
            settings,
            _parentSession.CurrentModel,
            history,
            _parentSession.PromptCachingEnabled,
            _parentSession.PromptCacheTtl);

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
            AgentOutput.Current.RenderInfo($"  ✓ {type} subagent complete ({result.Length} chars)");
            return result;
        }
        catch (OperationCanceledException)
        {
            return $"[{type} subagent cancelled]";
        }
#pragma warning disable CA1031 // non-fatal subagent failure
        catch (Exception ex)
        {
            AgentOutput.Current.RenderWarning($"  ⚠ {type} subagent failed: {ex.Message}");
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

    /// <summary>
    /// Spawns a subagent as a tracked concurrent task and returns immediately.
    /// The task executes asynchronously and is registered in the task registry if available.
    /// </summary>
    /// <param name="type">The subagent type.</param>
    /// <param name="prompt">The prompt to send to the subagent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="IAgentTask"/> representing the spawned subagent task.</returns>
    public async Task<IAgentTask> SpawnAsync(
        SubagentType type,
        string prompt,
        CancellationToken ct = default)
    {
        var taskId = GenerateTaskId();
        var description = $"Subagent: {type}";

        var task = new AgentTask(
            taskId,
            AgentTaskType.LocalAgent,
            AgentTaskStatus.Running,
            description,
            DateTimeOffset.UtcNow,
            ct,
            async innerCt =>
            {
                // Delegate to existing RunAsync for the actual execution
                var result = await RunAsync(type, prompt, innerCt).ConfigureAwait(false);
                return result;
            });

        if (_taskRegistry is not null)
        {
            await _taskRegistry.RegisterAsync(task, ct).ConfigureAwait(false);
        }

        // Fire and forget - execute asynchronously
        _ = ExecuteTaskAsync(task, ct);

        return task;
    }

    private async Task ExecuteTaskAsync(IAgentTask task, CancellationToken ct)
    {
        try
        {
            if (task is AgentTask at)
            {
                at.Status = AgentTaskStatus.Running;
            }

            await task.ExecuteAsync(ct).ConfigureAwait(false);

            if (task is AgentTask completedTask)
            {
                completedTask.Status = AgentTaskStatus.Completed;
            }
        }
        catch (OperationCanceledException)
        {
            if (task is AgentTask cancelledTask)
            {
                cancelledTask.Status = AgentTaskStatus.Cancelled;
            }
        }
        catch
        {
            if (task is AgentTask failedTask)
            {
                failedTask.Status = AgentTaskStatus.Failed;
            }
        }
    }

    private static string GenerateTaskId() => $"task-{Guid.NewGuid():N}"[..20];

    private Kernel BuildScopedKernel(SubagentType type)
    {
        // Clone the parent kernel's chat completion service
        var parentKernel = _parentSession.Kernel;
        var builder = Kernel.CreateBuilder();

        // Copy the chat completion service from parent
        var chatService = parentKernel.GetRequiredService<IChatCompletionService>();
        builder.Services.AddSingleton(chatService);

        var kernel = builder.Build();

        // Resolve the allowed plugin names for this subagent type
        IReadOnlySet<string> allowedPlugins;
        if (_loadoutRegistry is not null)
        {
            var loadoutName = GetLoadoutName(type);
            allowedPlugins = _loadoutRegistry.ResolveActivePlugins(
                loadoutName, parentKernel.Plugins);
        }
        else
        {
            allowedPlugins = GetDefaultPluginSet(type);
        }

        foreach (var plugin in parentKernel.Plugins)
        {
            if (allowedPlugins.Contains(plugin.Name))
            {
                kernel.Plugins.Add(plugin);
            }
        }

        return kernel;
    }

    /// <summary>
    /// Returns the name of the built-in <see cref="ToolLoadout"/> that maps to
    /// the given <see cref="SubagentType"/>.
    /// </summary>
    internal static string GetLoadoutName(SubagentType type) => type switch
    {
        SubagentType.Explore => WellKnownLoadouts.Research,
        SubagentType.Task => WellKnownLoadouts.Minimal,
        SubagentType.Plan => WellKnownLoadouts.Developer,
        SubagentType.Review => WellKnownLoadouts.Developer,
        SubagentType.General => WellKnownLoadouts.Full,
        _ => WellKnownLoadouts.Minimal,
    };

    /// <summary>
    /// Fallback plugin set used when no <see cref="IToolLoadoutRegistry"/> is provided.
    /// Plugin names must match those passed to <c>AddFromType</c> / <c>AddFromObject</c>.
    /// </summary>
    private static HashSet<string> GetDefaultPluginSet(SubagentType type) => type switch
    {
        SubagentType.Explore => ["file", "search", "git", "memory"],
        SubagentType.Task => ["shell", "file", "search"],
        SubagentType.Plan => ["file", "search", "memory", "git"],
        SubagentType.Review => ["file", "search", "git"],
        SubagentType.General => ["file", "search", "git", "shell", "web", "memory"],
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
