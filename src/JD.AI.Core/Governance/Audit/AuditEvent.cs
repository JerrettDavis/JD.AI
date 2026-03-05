namespace JD.AI.Core.Governance.Audit;

public sealed class AuditEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    public string? TraceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Resource { get; init; }
    public string? Detail { get; init; }
    public AuditSeverity Severity { get; init; } = AuditSeverity.Info;
    public PolicyDecision? PolicyResult { get; init; }

    /// <summary>Tool name for tool invocation events.</summary>
    public string? ToolName { get; init; }

    /// <summary>Redacted tool arguments for tool invocation events.</summary>
    public string? ToolArguments { get; init; }

    /// <summary>Tool result summary (truncated) for tool invocation events.</summary>
    public string? ToolResult { get; init; }

    /// <summary>Duration of the audited operation in milliseconds.</summary>
    public long? DurationMs { get; init; }

    /// <summary>Hash of the previous audit event for tamper-evident chaining. Null for the first event.</summary>
    public string? PreviousHash { get; init; }
}

public enum AuditSeverity { Debug, Info, Warning, Error, Critical }
