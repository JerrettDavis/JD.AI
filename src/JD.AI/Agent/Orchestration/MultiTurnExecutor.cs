using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Multi-turn subagent executor — runs its own conversation loop with tool calling,
/// continuing until the agent signals completion or hits the turn limit.
/// </summary>
public sealed class MultiTurnExecutor : ISubagentExecutor
{
    private const string DoneMarker = "[DONE]";

    public async Task<AgentResult> ExecuteAsync(
        SubagentConfig config,
        AgentSession parentSession,
        TeamContext? teamContext = null,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        var events = new List<AgentEvent>();
        var sw = Stopwatch.StartNew();
        var maxTurns = config.MaxTurns > 0 ? config.MaxTurns : 10;

        onProgress?.Invoke(new SubagentProgress(config.Name, SubagentStatus.Started));
        events.Add(new AgentEvent(config.Name, AgentEventType.Started, config.Prompt));
        teamContext?.RecordEvent(config.Name, AgentEventType.Started, config.Prompt);

        var kernel = SingleTurnExecutor.BuildScopedKernel(config, parentSession);

        // Inject team context tools if in a team
        if (teamContext != null)
        {
            var contextTools = new TeamContextTools(teamContext, config.Name);
            kernel.Plugins.AddFromObject(contextTools, "TeamContext");
        }

        var history = new ChatHistory();
        var systemPrompt = config.SystemPrompt ?? SubagentPrompts.GetSystemPrompt(config.Type);
        systemPrompt += $"""

            
            You are running in multi-turn mode. You have up to {maxTurns} turns to complete the task.
            Use tools as needed across multiple turns. When you are done, include "{DoneMarker}" in your final response.
            """;

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

        var allOutput = new StringBuilder();

        try
        {
            for (var turn = 0; turn < maxTurns; turn++)
            {
                ct.ThrowIfCancellationRequested();

                onProgress?.Invoke(new SubagentProgress(
                    config.Name, SubagentStatus.Thinking,
                    $"Turn {turn + 1}/{maxTurns}"));

                var turnResponse = new StringBuilder();
                await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
                    history, settings, kernel, ct).ConfigureAwait(false))
                {
                    if (chunk.Content is { Length: > 0 } text)
                    {
                        turnResponse.Append(text);
                    }
                }

                var responseText = turnResponse.ToString();
                if (responseText.Length > 0)
                {
                    history.AddAssistantMessage(responseText);
                    allOutput.AppendLine(responseText);

                    events.Add(new AgentEvent(config.Name, AgentEventType.Decision,
                        $"Turn {turn + 1}: {(responseText.Length > 100 ? string.Concat(responseText.AsSpan(0, 100), "...") : responseText)}"));
                    teamContext?.RecordEvent(config.Name, AgentEventType.Decision,
                        $"Turn {turn + 1}: {(responseText.Length > 100 ? string.Concat(responseText.AsSpan(0, 100), "...") : responseText)}");
                }

                // Check if agent signals completion
                if (responseText.Contains(DoneMarker, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // If not done, add a continuation prompt
                if (turn < maxTurns - 1)
                {
                    history.AddUserMessage("Continue. If you're done, include [DONE] in your response.");
                }
            }

            sw.Stop();
            var output = allOutput.Length > 0 ? allOutput.ToString().Trim() : "(no response)";

            events.Add(new AgentEvent(config.Name, AgentEventType.Completed, $"{output.Length} chars"));
            teamContext?.RecordEvent(config.Name, AgentEventType.Completed, $"{output.Length} chars");

            var result = new AgentResult
            {
                AgentName = config.Name,
                Output = output,
                Duration = sw.Elapsed,
                Events = events,
            };
            teamContext?.SetResult(result);

            onProgress?.Invoke(new SubagentProgress(
                config.Name, SubagentStatus.Completed,
                $"{output.Length} chars", Elapsed: sw.Elapsed));

            return result;
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
                Output = allOutput.ToString(),
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
                Output = allOutput.ToString(),
                Success = false,
                Error = ex.Message,
                Duration = sw.Elapsed,
                Events = events,
            };
        }
#pragma warning restore CA1031
    }
}
