namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// Thread-safe in-memory audit sink that stores the most recent events
/// in a circular buffer and supports filtered queries.
/// </summary>
public sealed class InMemoryAuditSink : IQueryableAuditSink
{
    private readonly AuditEvent[] _buffer;
    private readonly Lock _lock = new();
    private long _writeIndex;

    /// <param name="capacity">Maximum number of events to retain (default 10,000).</param>
    public InMemoryAuditSink(int capacity = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        _buffer = new AuditEvent[capacity];
    }

    public string Name => "memory";

    public long Count
    {
        get
        {
            lock (_lock)
                return Math.Min(_writeIndex, _buffer.Length);
        }
    }

    public Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        lock (_lock)
        {
            var index = (int)(_writeIndex % _buffer.Length);
            _buffer[index] = evt;
            _writeIndex++;
        }

        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, 1000);

        List<AuditEvent> snapshot;
        lock (_lock)
        {
            var count = (int)Math.Min(_writeIndex, _buffer.Length);
            snapshot = new List<AuditEvent>(count);

            // Read in reverse chronological order (newest first)
            for (var i = 0; i < count; i++)
            {
                var idx = (int)((_writeIndex - 1 - i) % _buffer.Length);
                if (idx < 0) idx += _buffer.Length;
                snapshot.Add(_buffer[idx]);
            }
        }

        // Apply filters
        IEnumerable<AuditEvent> filtered = snapshot;

        if (query.Action is not null)
            filtered = filtered.Where(e => string.Equals(e.Action, query.Action, StringComparison.OrdinalIgnoreCase));

        if (query.MinSeverity is { } minSev)
            filtered = filtered.Where(e => e.Severity >= minSev);

        if (query.SessionId is not null)
            filtered = filtered.Where(e => string.Equals(e.SessionId, query.SessionId, StringComparison.Ordinal));

        if (query.UserId is not null)
            filtered = filtered.Where(e => string.Equals(e.UserId, query.UserId, StringComparison.Ordinal));

        if (query.Resource is not null)
            filtered = filtered.Where(e => e.Resource is not null &&
                e.Resource.Contains(query.Resource, StringComparison.OrdinalIgnoreCase));

        if (query.From is { } from)
            filtered = filtered.Where(e => e.Timestamp >= from);

        if (query.Until is { } until)
            filtered = filtered.Where(e => e.Timestamp < until);

        if (query.TenantId is not null)
            filtered = filtered.Where(e => string.Equals(e.TenantId, query.TenantId, StringComparison.OrdinalIgnoreCase));

        var matchedList = filtered.ToList();
        var totalCount = matchedList.Count;

        var page = matchedList
            .Skip(query.Offset)
            .Take(limit)
            .ToList();

        return Task.FromResult(new AuditQueryResult
        {
            Events = page,
            TotalCount = totalCount,
        });
    }
}
