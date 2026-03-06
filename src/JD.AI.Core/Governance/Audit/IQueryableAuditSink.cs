namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// An <see cref="IAuditSink"/> that also supports querying stored events.
/// </summary>
public interface IQueryableAuditSink : IAuditSink
{
    /// <summary>
    /// Queries stored audit events with optional filters.
    /// Results are returned in reverse chronological order (newest first).
    /// </summary>
    Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct = default);

    /// <summary>Returns the total number of stored events.</summary>
    long Count { get; }
}

/// <summary>Filter criteria for querying audit events.</summary>
public sealed class AuditQuery
{
    /// <summary>Filter by action (exact match, case-insensitive).</summary>
    public string? Action { get; init; }

    /// <summary>Filter by minimum severity level.</summary>
    public AuditSeverity? MinSeverity { get; init; }

    /// <summary>Filter by session ID (exact match).</summary>
    public string? SessionId { get; init; }

    /// <summary>Filter by user ID (exact match).</summary>
    public string? UserId { get; init; }

    /// <summary>Filter by resource (contains, case-insensitive).</summary>
    public string? Resource { get; init; }

    /// <summary>Only return events at or after this timestamp.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only return events before this timestamp.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Maximum number of events to return (default 50, max 1000).</summary>
    public int Limit { get; init; } = 50;

    /// <summary>Number of events to skip for pagination.</summary>
    public int Offset { get; init; }

    /// <summary>Filter by tenant ID. When set, only events from this tenant are returned.</summary>
    public string? TenantId { get; init; }
}

/// <summary>Result of an audit event query.</summary>
public sealed class AuditQueryResult
{
    /// <summary>The matching events (newest first).</summary>
    public required IReadOnlyList<AuditEvent> Events { get; init; }

    /// <summary>Total number of events matching the filter (before limit/offset).</summary>
    public required long TotalCount { get; init; }
}
