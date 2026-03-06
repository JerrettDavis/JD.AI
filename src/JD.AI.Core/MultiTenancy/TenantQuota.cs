using System.Collections.Concurrent;

namespace JD.AI.Core.MultiTenancy;

/// <summary>
/// Per-tenant resource quotas and rate limits. Tracks usage
/// per tenant within a configurable time window.
/// </summary>
public sealed class TenantQuota
{
    private readonly ConcurrentDictionary<string, TenantUsage> _usage = new(StringComparer.OrdinalIgnoreCase);
    private readonly TenantQuotaConfig _config;

    public TenantQuota(TenantQuotaConfig? config = null)
    {
        _config = config ?? new TenantQuotaConfig();
    }

    /// <summary>
    /// Checks if the tenant has exceeded their request quota.
    /// </summary>
    public bool IsOverRequestQuota(string tenantId)
    {
        if (!_usage.TryGetValue(tenantId, out var usage))
            return false;

        usage.PruneExpired(_config.WindowDuration);

        return _config.MaxRequestsPerWindow > 0
               && usage.RequestCount >= _config.MaxRequestsPerWindow;
    }

    /// <summary>
    /// Checks if the tenant has exceeded their daily token budget.
    /// </summary>
    public bool IsOverTokenQuota(string tenantId)
    {
        if (_config.MaxTokensPerDay <= 0) return false;
        if (!_usage.TryGetValue(tenantId, out var usage)) return false;

        return usage.DailyTokens >= _config.MaxTokensPerDay;
    }

    /// <summary>
    /// Checks if the tenant has exceeded their concurrent session limit.
    /// </summary>
    public bool IsOverSessionQuota(string tenantId)
    {
        if (_config.MaxConcurrentSessions <= 0) return false;
        if (!_usage.TryGetValue(tenantId, out var usage)) return false;

        return usage.ConcurrentSessions >= _config.MaxConcurrentSessions;
    }

    /// <summary>
    /// Records a request for the given tenant.
    /// </summary>
    public void RecordRequest(string tenantId)
    {
        var usage = _usage.GetOrAdd(tenantId, _ => new TenantUsage());
        usage.AddRequest();
    }

    /// <summary>
    /// Records token consumption for the given tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="tokens">Number of tokens consumed.</param>
    public void RecordTokens(string tenantId, long tokens)
    {
        if (tokens <= 0) return;
        var usage = _usage.GetOrAdd(tenantId, _ => new TenantUsage());
        usage.AddTokens(tokens);
    }

    /// <summary>
    /// Increments the concurrent session count for the given tenant.
    /// Call <see cref="ReleaseSession"/> when the session ends.
    /// </summary>
    public void AcquireSession(string tenantId)
    {
        var usage = _usage.GetOrAdd(tenantId, _ => new TenantUsage());
        usage.IncrementSessions();
    }

    /// <summary>
    /// Decrements the concurrent session count for the given tenant.
    /// </summary>
    public void ReleaseSession(string tenantId)
    {
        if (_usage.TryGetValue(tenantId, out var usage))
        {
            usage.DecrementSessions();
        }
    }

    /// <summary>
    /// Gets current usage stats for a tenant.
    /// </summary>
    public TenantUsageStats GetUsage(string tenantId)
    {
        if (!_usage.TryGetValue(tenantId, out var usage))
            return new TenantUsageStats(0, _config.MaxRequestsPerWindow, _config.WindowDuration, 0, _config.MaxTokensPerDay, 0, _config.MaxConcurrentSessions);

        usage.PruneExpired(_config.WindowDuration);
        return new TenantUsageStats(
            usage.RequestCount,
            _config.MaxRequestsPerWindow,
            _config.WindowDuration,
            usage.DailyTokens,
            _config.MaxTokensPerDay,
            usage.ConcurrentSessions,
            _config.MaxConcurrentSessions);
    }
}

/// <summary>Configuration for per-tenant quotas.</summary>
public sealed class TenantQuotaConfig
{
    /// <summary>Maximum requests per tenant per window. 0 = unlimited.</summary>
    public int MaxRequestsPerWindow { get; init; } = 1000;

    /// <summary>Rolling window duration for quota tracking.</summary>
    public TimeSpan WindowDuration { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Maximum tokens consumed per day. 0 = unlimited.</summary>
    public long MaxTokensPerDay { get; init; }

    /// <summary>Maximum concurrent agent sessions per tenant. 0 = unlimited.</summary>
    public int MaxConcurrentSessions { get; init; }

    /// <summary>Action taken when any quota threshold is exceeded.</summary>
    public QuotaExceededAction OnQuotaExceeded { get; init; } = QuotaExceededAction.Reject;
}

/// <summary>Action taken when a tenant exceeds a quota.</summary>
public enum QuotaExceededAction
{
    /// <summary>Reject the request with an error.</summary>
    Reject,

    /// <summary>Queue the request until quota resets.</summary>
    Queue,

    /// <summary>Allow the request but degrade to a smaller/cheaper model.</summary>
    Degrade,
}

/// <summary>Current usage stats for a tenant.</summary>
public sealed record TenantUsageStats(
    int CurrentRequests,
    int MaxRequests,
    TimeSpan WindowDuration,
    long CurrentTokensToday,
    long MaxTokensPerDay,
    int CurrentSessions,
    int MaxConcurrentSessions);

/// <summary>Thread-safe per-tenant usage tracker.</summary>
internal sealed class TenantUsage
{
    private readonly Lock _lock = new();
    private readonly List<DateTimeOffset> _timestamps = [];
    private long _dailyTokens;
    private int _concurrentSessions;

    public int RequestCount
    {
        get { lock (_lock) return _timestamps.Count; }
    }

    public long DailyTokens => Interlocked.Read(ref _dailyTokens);

    public int ConcurrentSessions => Volatile.Read(ref _concurrentSessions);

    public void AddRequest()
    {
        lock (_lock)
        {
            _timestamps.Add(DateTimeOffset.UtcNow);
        }
    }

    public void AddTokens(long tokens) => Interlocked.Add(ref _dailyTokens, tokens);

    public void IncrementSessions() => Interlocked.Increment(ref _concurrentSessions);

    public void DecrementSessions()
    {
        var current = Volatile.Read(ref _concurrentSessions);
        if (current > 0) Interlocked.Decrement(ref _concurrentSessions);
    }

    public void PruneExpired(TimeSpan window)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        lock (_lock)
        {
            _timestamps.RemoveAll(t => t < cutoff);
        }
    }
}
