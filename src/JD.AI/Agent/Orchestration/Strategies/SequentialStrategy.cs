using System.Diagnostics;

namespace JD.AI.Tui.Agent.Orchestration.Strategies;

/// <summary>
/// Sequential pipeline — agents run in order, each receiving the previous agent's
/// output appended to its prompt plus access to the shared scratchpad.
/// Good for: explore → plan → implement → review pipelines.
/// </summary>
public sealed class SequentialStrategy : IOrchestrationStrategy
{
    public string Name => "sequential";

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
        string? previousOutput = null;

        foreach (var agent in agents)
        {
            ct.ThrowIfCancellationRequested();

            // Augment prompt with previous agent's output
            var augmentedConfig = agent;
            if (previousOutput != null)
            {
                augmentedConfig = new SubagentConfig
                {
                    Name = agent.Name,
                    Type = agent.Type,
                    Prompt = $"{agent.Prompt}\n\n--- Previous agent output ---\n{previousOutput}",
                    SystemPrompt = agent.SystemPrompt,
                    MaxTurns = agent.MaxTurns,
                    ModelId = agent.ModelId,
                    AdditionalTools = agent.AdditionalTools,
                    Perspective = agent.Perspective,
                };
            }

            var result = await executor.ExecuteAsync(
                augmentedConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

            results[agent.Name] = result;
            previousOutput = result.Output;

            // Store in scratchpad for later agents to reference
            context.WriteScratchpad($"output:{agent.Name}", result.Output);
        }

        sw.Stop();

        return new TeamResult
        {
            Output = previousOutput ?? "(no output)",
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = results.Values.All(r => r.Success),
        };
    }
}
