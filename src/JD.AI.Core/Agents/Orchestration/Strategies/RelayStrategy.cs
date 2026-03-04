using System.Diagnostics;

namespace JD.AI.Core.Agents.Orchestration.Strategies;

/// <summary>
/// Relay strategy — agents process sequentially, each refining the previous output.
/// Like a relay race where each runner adds their specialized perspective.
/// Good for: document writing, iterative improvement, multi-perspective analysis.
/// </summary>
public sealed class RelayStrategy : IOrchestrationStrategy
{
    public string Name => "relay";

    /// <summary>
    /// Template for prompts passed to each relay agent.
    /// Supports {previous_output} and {focus_area} placeholders.
    /// </summary>
    public string PassPromptTemplate { get; init; } =
        "Refine and improve this output. Focus on {focus_area}.\n\nPrevious output:\n{previous_output}";

    /// <summary>When true, stop early if an agent reports no changes needed.</summary>
    public bool StopEarly { get; init; } = true;

    public async Task<TeamResult> ExecuteAsync(
        IReadOnlyList<SubagentConfig> agents,
        TeamContext context,
        ISubagentExecutor executor,
        AgentSession parentSession,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new Dictionary<string, AgentResult>(StringComparer.Ordinal);
        var currentOutput = context.Goal; // First agent gets the original goal

        for (var i = 0; i < agents.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var agent = agents[i];
            var focusArea = agent.Perspective ?? agent.Name;

            // Build relay prompt from template
            var relayPrompt = PassPromptTemplate
                .Replace("{previous_output}", currentOutput, StringComparison.Ordinal)
                .Replace("{focus_area}", focusArea, StringComparison.Ordinal);

            var relayConfig = new SubagentConfig
            {
                Name = agent.Name,
                Type = agent.Type,
                Prompt = relayPrompt,
                SystemPrompt = agent.SystemPrompt ?? $"""
                    You are relay agent #{i + 1} of {agents.Count} in a sequential refinement pipeline.
                    Your focus area is: {focusArea}.
                    Review the previous output and improve it based on your expertise.
                    If no changes are needed, respond with exactly "[NO_CHANGES]" and nothing else.
                    Otherwise, provide the complete improved output.
                    """,
                MaxTurns = agent.MaxTurns,
                ModelId = agent.ModelId,
                AdditionalTools = agent.AdditionalTools,
            };

            var result = await executor.ExecuteAsync(
                relayConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

            results[agent.Name] = result;
            context.WriteScratchpad($"relay:{i}:{agent.Name}", result.Output);

            // Check for early stop
            if (StopEarly && result.Output.Contains("[NO_CHANGES]", StringComparison.OrdinalIgnoreCase))
            {
                context.RecordEvent(new AgentEvent(
                    agent.Name, AgentEventType.Decision,
                    $"Relay stopped early at agent {i + 1}/{agents.Count}: no changes needed"));
                break;
            }

            currentOutput = result.Output;
        }

        sw.Stop();

        return new TeamResult
        {
            Output = currentOutput,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = results.Values.All(r => r.Success),
        };
    }
}
