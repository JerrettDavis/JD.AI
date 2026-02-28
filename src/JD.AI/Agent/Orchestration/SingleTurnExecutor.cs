using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Single-turn subagent executor — sends one prompt and returns the response.
/// Equivalent to the original SubagentRunner.RunAsync behavior.
/// </summary>
public sealed class SingleTurnExecutor : ISubagentExecutor
{
    public async Task<AgentResult> ExecuteAsync(
        SubagentConfig config,
        AgentSession parentSession,
        TeamContext? teamContext = null,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        var events = new List<AgentEvent>();
        var sw = Stopwatch.StartNew();

        onProgress?.Invoke(new SubagentProgress(config.Name, SubagentStatus.Started));
        events.Add(new AgentEvent(config.Name, AgentEventType.Started, config.Prompt));
        teamContext?.RecordEvent(config.Name, AgentEventType.Started, config.Prompt);

        var kernel = BuildScopedKernel(config, parentSession);
        var history = new ChatHistory();

        var systemPrompt = config.SystemPrompt ?? SubagentPrompts.GetSystemPrompt(config.Type);
        if (teamContext != null)
        {
            systemPrompt += "\n\n" + teamContext.ToPromptSummary();
        }

        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(config.Prompt);

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4096,
        };

        try
        {
            onProgress?.Invoke(new SubagentProgress(config.Name, SubagentStatus.Thinking));

            var fullResponse = new StringBuilder();
            await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                history, settings, kernel, ct).ConfigureAwait(false))
            {
                if (chunk.Content is { Length: > 0 } text)
                {
                    fullResponse.Append(text);
                }
            }

            sw.Stop();
            var output = fullResponse.Length > 0 ? fullResponse.ToString() : "(no response)";

            events.Add(new AgentEvent(config.Name, AgentEventType.Completed, $"{output.Length} chars"));
            teamContext?.RecordEvent(config.Name, AgentEventType.Completed, $"{output.Length} chars");
            teamContext?.SetResult(new AgentResult
            {
                AgentName = config.Name,
                Output = output,
                Duration = sw.Elapsed,
                Events = events,
            });

            onProgress?.Invoke(new SubagentProgress(
                config.Name, SubagentStatus.Completed,
                $"{output.Length} chars", Elapsed: sw.Elapsed));

            return new AgentResult
            {
                AgentName = config.Name,
                Output = output,
                Duration = sw.Elapsed,
                Events = events,
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            events.Add(new AgentEvent(config.Name, AgentEventType.Cancelled, "Cancelled"));
            teamContext?.RecordEvent(config.Name, AgentEventType.Cancelled, "Cancelled");
            onProgress?.Invoke(new SubagentProgress(config.Name, SubagentStatus.Cancelled));

            return new AgentResult
            {
                AgentName = config.Name,
                Output = "[cancelled]",
                Success = false,
                Error = "Cancelled",
                Duration = sw.Elapsed,
                Events = events,
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            sw.Stop();
            events.Add(new AgentEvent(config.Name, AgentEventType.Error, ex.Message));
            teamContext?.RecordEvent(config.Name, AgentEventType.Error, ex.Message);
            onProgress?.Invoke(new SubagentProgress(config.Name, SubagentStatus.Failed, ex.Message));

            return new AgentResult
            {
                AgentName = config.Name,
                Output = string.Empty,
                Success = false,
                Error = ex.Message,
                Duration = sw.Elapsed,
                Events = events,
            };
        }
#pragma warning restore CA1031
    }

    internal static Kernel BuildScopedKernel(SubagentConfig config, AgentSession parentSession)
    {
        var parentKernel = parentSession.Kernel;
        var builder = Kernel.CreateBuilder();

        var chatService = parentKernel.GetRequiredService<IChatCompletionService>();
        builder.Services.AddSingleton(chatService);

        var kernel = builder.Build();

        var toolSets = SubagentPrompts.GetToolSet(config.Type);
        if (config.AdditionalTools is { Count: > 0 })
        {
            toolSets.UnionWith(config.AdditionalTools);
        }

        foreach (var plugin in parentKernel.Plugins)
        {
            if (toolSets.Contains(plugin.Name, StringComparer.OrdinalIgnoreCase))
            {
                kernel.Plugins.Add(plugin);
            }
        }

        return kernel;
    }
}
