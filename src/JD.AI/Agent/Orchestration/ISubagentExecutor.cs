namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>
/// Configuration for a subagent to be executed.
/// </summary>
public sealed class SubagentConfig
{
    /// <summary>Unique name for this agent within a team.</summary>
    public required string Name { get; init; }

    /// <summary>The subagent type controlling tool scoping and system prompt.</summary>
    public SubagentType Type { get; init; } = SubagentType.General;

    /// <summary>The prompt/task for this agent.</summary>
    public required string Prompt { get; init; }

    /// <summary>Optional custom system prompt (overrides the default for the type).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Maximum number of turns for multi-turn execution.</summary>
    public int MaxTurns { get; init; } = 10;

    /// <summary>Optional model preference override.</summary>
    public string? ModelId { get; init; }

    /// <summary>Optional additional tool plugin names to include.</summary>
    public IReadOnlyList<string>? AdditionalTools { get; init; }

    /// <summary>Perspective label for debate strategy (e.g., "optimist", "skeptic").</summary>
    public string? Perspective { get; init; }
}

/// <summary>
/// Abstraction over single-turn and multi-turn subagent execution.
/// </summary>
public interface ISubagentExecutor
{
    /// <summary>
    /// Execute a subagent with the given configuration and optional team context.
    /// </summary>
    /// <param name="config">Agent configuration (type, prompt, tools, etc.)</param>
    /// <param name="parentSession">Parent agent session for kernel/service access.</param>
    /// <param name="teamContext">Optional team context for shared state. Null for standalone agents.</param>
    /// <param name="onProgress">Optional callback for real-time progress updates.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's execution result.</returns>
    Task<AgentResult> ExecuteAsync(
        SubagentConfig config,
        AgentSession parentSession,
        TeamContext? teamContext = null,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default);
}

/// <summary>
/// The combined result of a team execution.
/// </summary>
public sealed class TeamResult
{
    /// <summary>The synthesized/final output from the team.</summary>
    public required string Output { get; init; }

    /// <summary>The orchestration strategy used.</summary>
    public required string Strategy { get; init; }

    /// <summary>Per-agent results.</summary>
    public IReadOnlyDictionary<string, AgentResult> AgentResults { get; init; } =
        new Dictionary<string, AgentResult>(StringComparer.Ordinal);

    /// <summary>Total tokens consumed across all agents.</summary>
    public long TotalTokens => AgentResults.Values.Sum(r => r.TokensUsed);

    /// <summary>Total wall-clock duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Whether the team completed successfully.</summary>
    public bool Success { get; init; } = true;
}
