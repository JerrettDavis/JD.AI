using System.Diagnostics;

namespace JD.AI.Core.Agents.Orchestration.Strategies;

/// <summary>
/// Voting strategy — N agents independently process the same input.
/// Results are aggregated by configurable voting (majority, weighted confidence).
/// Good for: code review consensus, classification, risk assessment.
/// </summary>
public sealed class VotingStrategy : IOrchestrationStrategy
{
    public string Name => "voting";

    /// <summary>Voting method to determine the final result.</summary>
    public VotingMethod Method { get; init; } = VotingMethod.Majority;

    /// <summary>Per-agent weight multipliers (optional). Key = agent name.</summary>
    public IReadOnlyDictionary<string, double>? Weights { get; init; }

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

        // All agents process the same goal independently (parallel)
        var tasks = agents.Select(agent =>
            executor.ExecuteAsync(agent, parentSession, context, onProgress, ct));

        var agentResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in agentResults)
        {
            results[result.AgentName] = result;
            context.WriteScratchpad($"vote:{result.AgentName}", result.Output);
        }

        // Aggregate using a synthesizer that sees all votes
        var aggregationPrompt = BuildAggregationPrompt(context, agentResults);
        var aggregatorConfig = new SubagentConfig
        {
            Name = "vote-aggregator",
            Type = SubagentType.General,
            Prompt = aggregationPrompt,
            SystemPrompt = $"""
                You are a vote aggregation agent. You have received independent analyses from
                {agentResults.Length} agents on the same input. Your job is to:
                1. Identify areas of consensus (most agents agree)
                2. Identify areas of disagreement (agents differ)
                3. Apply {Method} voting to resolve disagreements
                4. Produce a final synthesized result that reflects the team consensus
                5. Note any minority opinions that are worth considering
                Be precise and attribute conclusions to the consensus level (unanimous, majority, split).
                """,
            MaxTurns = 1,
        };

        var aggregation = await executor.ExecuteAsync(
            aggregatorConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

        results["vote-aggregator"] = aggregation;
        sw.Stop();

        return new TeamResult
        {
            Output = aggregation.Output,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = agentResults.All(r => r.Success),
        };
    }

    private string BuildAggregationPrompt(TeamContext context, AgentResult[] results)
    {
        var parts = new List<string>
        {
            $"Team goal: {context.Goal}",
            $"Voting method: {Method}",
            "",
            $"{results.Length} agents have independently analyzed the same input. Their responses:",
            "",
        };

        foreach (var result in results)
        {
            var weight = Weights is not null &&
                         Weights.TryGetValue(result.AgentName, out var w) ? w : 1.0;
            parts.Add($"--- Agent: {result.AgentName} (weight={weight:F1}, success={result.Success}) ---");
            parts.Add(result.Output);
            parts.Add("");
        }

        parts.Add($"Apply {Method} voting to produce a unified result. Attribute confidence levels.");
        return string.Join('\n', parts);
    }
}

/// <summary>Method for aggregating votes.</summary>
public enum VotingMethod
{
    /// <summary>Simple majority wins.</summary>
    Majority,

    /// <summary>Weighted by agent confidence/weight.</summary>
    WeightedConfidence,

    /// <summary>All agents must agree.</summary>
    Unanimous,
}
