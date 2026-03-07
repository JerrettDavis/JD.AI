using System.Diagnostics;

namespace JD.AI.Core.Agents.Orchestration.Strategies;

/// <summary>
/// Blackboard strategy — agents operate on a shared knowledge base (the scratchpad)
/// in iterative rounds. Each round, agents read the current blackboard state,
/// contribute their analysis, and write results back. Converges when no agent
/// contributes new information or max iterations reached.
/// Good for: complex analysis, multi-perspective investigation, knowledge synthesis.
/// </summary>
public sealed class BlackboardStrategy : IOrchestrationStrategy
{
    public string Name => "blackboard";

    /// <summary>Maximum number of convergence rounds.</summary>
    public int MaxIterations { get; init; } = 3;

    /// <summary>
    /// Template for the blackboard prompt passed to each agent.
    /// Supports {blackboard_state} and {agent_role} placeholders.
    /// </summary>
    public string BoardPromptTemplate { get; init; } =
        """
        You are a specialist agent contributing to a collaborative analysis.
        Your role: {agent_role}

        Current shared knowledge (blackboard):
        {blackboard_state}

        Original goal: {goal}

        Review the existing knowledge, add your analysis, correct errors, and fill gaps.
        Write ONLY your new contributions. If you have nothing new to add, respond with exactly "[CONVERGED]".
        """;

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

        // Initialize the blackboard with the goal
        context.WriteScratchpad("blackboard:state", context.Goal);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            var boardState = context.ReadScratchpad("blackboard:state") ?? "";
            var convergedCount = 0;
            var roundContributions = new List<string>();

            for (var i = 0; i < agents.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var agent = agents[i];
                var agentRole = agent.Perspective ?? agent.Name;

                var prompt = BoardPromptTemplate
                    .Replace("{blackboard_state}", boardState, StringComparison.Ordinal)
                    .Replace("{agent_role}", agentRole, StringComparison.Ordinal)
                    .Replace("{goal}", context.Goal, StringComparison.Ordinal);

                var boardConfig = new SubagentConfig
                {
                    Name = agent.Name,
                    Type = agent.Type,
                    Prompt = prompt,
                    SystemPrompt = agent.SystemPrompt,
                    MaxTurns = agent.MaxTurns,
                    ModelId = agent.ModelId,
                    AdditionalTools = agent.AdditionalTools,
                };

                var result = await executor.ExecuteAsync(
                    boardConfig, parentSession, context, onProgress, ct).ConfigureAwait(false);

                var key = $"blackboard:{iteration}:{agent.Name}";
                results[key] = result;
                context.WriteScratchpad(key, result.Output);

                if (result.Output.Contains("[CONVERGED]", StringComparison.OrdinalIgnoreCase))
                {
                    convergedCount++;
                }
                else
                {
                    roundContributions.Add($"[{agentRole}]: {result.Output}");
                }
            }

            // Update the blackboard state with new contributions
            if (roundContributions.Count > 0)
            {
                var newState = $"{boardState}\n\n--- Round {iteration + 1} ---\n{string.Join("\n\n", roundContributions)}";
                context.WriteScratchpad("blackboard:state", newState);
            }

            context.RecordEvent(new AgentEvent(
                "blackboard", AgentEventType.Decision,
                $"Round {iteration + 1}/{MaxIterations}: {roundContributions.Count} contributions, {convergedCount} converged"));

            // Check convergence — all agents signaled [CONVERGED]
            if (convergedCount == agents.Count)
            {
                context.RecordEvent(new AgentEvent(
                    "blackboard", AgentEventType.Decision,
                    $"Converged after {iteration + 1} round(s)"));
                break;
            }
        }

        sw.Stop();

        var finalState = context.ReadScratchpad("blackboard:state") ?? "";

        return new TeamResult
        {
            Output = finalState,
            Strategy = Name,
            AgentResults = results,
            Duration = sw.Elapsed,
            Success = results.Values.All(r => r.Success),
        };
    }
}
