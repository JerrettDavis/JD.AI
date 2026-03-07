namespace JD.AI.Core.Tracing;

/// <summary>
/// Ambient execution context that flows through all operations via <see cref="AsyncLocal{T}"/>.
/// Carries W3C trace IDs, session IDs, and turn metadata for end-to-end traceability.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>W3C trace ID (32 hex chars).</summary>
    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Current span ID (16 hex chars).</summary>
    public string SpanId { get; set; } = Guid.NewGuid().ToString("N")[..16];

    /// <summary>Parent span ID for hierarchical tracing.</summary>
    public string? ParentSpanId { get; set; }

    /// <summary>Current session ID.</summary>
    public string? SessionId { get; init; }

    /// <summary>Current turn index.</summary>
    public int TurnIndex { get; init; }

    /// <summary>Parent agent ID for multi-agent tracing.</summary>
    public string? ParentAgentId { get; set; }

    /// <summary>The execution timeline recording all operations in this turn.</summary>
    public ExecutionTimeline Timeline { get; } = new();

    /// <summary>Sentinel empty context.</summary>
    public static ExecutionContext Empty { get; } = new();

    /// <summary>
    /// Creates a child context that shares the same trace ID and session metadata
    /// but has a new span ID and records the parent span ID.
    /// </summary>
    internal ExecutionContext CreateChild(string childSpanId) => new()
    {
        TraceId = TraceId,
        SessionId = SessionId,
        TurnIndex = TurnIndex,
        ParentAgentId = ParentAgentId,
        SpanId = childSpanId,
        ParentSpanId = SpanId,
    };
}

/// <summary>
/// Ambient <see cref="AsyncLocal{T}"/> holder for the current <see cref="ExecutionContext"/>.
/// </summary>
public static class TraceContext
{
    private static readonly AsyncLocal<ExecutionContext?> Current = new();

    /// <summary>Gets or sets the current execution context for this async flow.</summary>
    public static ExecutionContext CurrentContext
    {
        get => Current.Value ?? ExecutionContext.Empty;
        set => Current.Value = value;
    }

    /// <summary>
    /// Starts a new trace for a user turn, setting the ambient context.
    /// </summary>
    public static ExecutionContext StartTurn(string? sessionId, int turnIndex)
    {
        var ctx = new ExecutionContext
        {
            SessionId = sessionId,
            TurnIndex = turnIndex,
        };
        Current.Value = ctx;
        return ctx;
    }

    /// <summary>
    /// Creates a child span under the current context.
    /// Allocates a new <see cref="ExecutionContext"/> so each async flow gets its own
    /// span state — avoids the read-modify-write race on the shared mutable object.
    /// </summary>
    public static string StartChildSpan()
    {
        var parent = CurrentContext;
        var childSpan = Guid.NewGuid().ToString("N")[..16];

        // Replace the AsyncLocal slot with a new object: preserves TraceId + session
        // metadata while assigning a new SpanId and recording the parent SpanId.
        Current.Value = parent.CreateChild(childSpan);

        return childSpan;
    }
}
