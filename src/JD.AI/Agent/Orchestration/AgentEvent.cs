namespace JD.AI.Tui.Agent.Orchestration;

/// <summary>Type of event emitted by a subagent during execution.</summary>
public enum AgentEventType
{
    /// <summary>Agent started executing.</summary>
    Started,

    /// <summary>Agent is thinking/reasoning.</summary>
    Thinking,

    /// <summary>Agent invoked a tool.</summary>
    ToolCall,

    /// <summary>Agent recorded a finding or observation.</summary>
    Finding,

    /// <summary>Agent made a decision.</summary>
    Decision,

    /// <summary>Agent encountered a non-fatal error.</summary>
    Error,

    /// <summary>Agent completed execution.</summary>
    Completed,

    /// <summary>Agent was cancelled.</summary>
    Cancelled,
}

/// <summary>
/// An event emitted by a subagent during execution, recorded in the team event stream.
/// </summary>
public sealed record AgentEvent(
    string AgentName,
    AgentEventType EventType,
    string Content,
    DateTime Timestamp)
{
    public AgentEvent(string agentName, AgentEventType eventType, string content)
        : this(agentName, eventType, content, DateTime.UtcNow) { }
}

/// <summary>Status of a subagent for the live progress panel.</summary>
public enum SubagentStatus
{
    /// <summary>Agent is queued but not yet started.</summary>
    Pending,

    /// <summary>Agent has started.</summary>
    Started,

    /// <summary>Agent is generating reasoning/thinking.</summary>
    Thinking,

    /// <summary>Agent is executing a tool.</summary>
    ExecutingTool,

    /// <summary>Agent completed successfully.</summary>
    Completed,

    /// <summary>Agent failed.</summary>
    Failed,

    /// <summary>Agent was cancelled.</summary>
    Cancelled,
}

/// <summary>
/// Real-time progress snapshot for a subagent, consumed by the progress panel.
/// </summary>
public sealed record SubagentProgress(
    string AgentName,
    SubagentStatus Status,
    string? Detail = null,
    int? TokensUsed = null,
    TimeSpan? Elapsed = null);

/// <summary>
/// The result produced by a single subagent execution.
/// </summary>
public sealed class AgentResult
{
    public required string AgentName { get; init; }
    public required string Output { get; init; }
    public bool Success { get; init; } = true;
    public string? Error { get; init; }
    public long TokensUsed { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<AgentEvent> Events { get; init; } = [];
}
