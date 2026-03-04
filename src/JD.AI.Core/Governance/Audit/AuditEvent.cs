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
}

public enum AuditSeverity { Debug, Info, Warning, Error, Critical }
