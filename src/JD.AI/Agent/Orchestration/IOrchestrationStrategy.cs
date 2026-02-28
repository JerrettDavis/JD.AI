namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Strategy for orchestrating a team of subagents.
/// </summary>
public interface IOrchestrationStrategy
{
    /// <summary>The strategy name (sequential, fan-out, supervisor, debate).</summary>
    string Name { get; }

    /// <summary>
    /// Execute the orchestration strategy with the given agents, context, and executor.
    /// </summary>
    Task<TeamResult> ExecuteAsync(
        IReadOnlyList<SubagentConfig> agents,
        TeamContext context,
        ISubagentExecutor executor,
        AgentSession parentSession,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default);
}
