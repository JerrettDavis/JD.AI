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
    public bool IsOverQuota(string tenantId)
    {
        if (!_usage.TryGetValue(tenantId, out var usage))
            return false;

        usage.PruneExpired(_config.WindowDuration);

        return _config.MaxRequestsPerWindow > 0
               && usage.RequestCount >= _config.MaxRequestsPerWindow;
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
    /// Gets current usage stats for a tenant.
    /// </summary>
    public TenantUsageStats GetUsage(string tenantId)
    {
        if (!_usage.TryGetValue(tenantId, out var usage))
            return new TenantUsageStats(0, _config.MaxRequestsPerWindow, _config.WindowDuration);

        usage.PruneExpired(_config.WindowDuration);
        return new TenantUsageStats(
            usage.RequestCount,
            _config.MaxRequestsPerWindow,
            _config.WindowDuration);
    }
}

/// <summary>Configuration for per-tenant quotas.</summary>
public sealed class TenantQuotaConfig
{
    /// <summary>Maximum requests per tenant per window. 0 = unlimited.</summary>
    public int MaxRequestsPerWindow { get; init; } = 1000;

    /// <summary>Rolling window duration for quota tracking.</summary>
    public TimeSpan WindowDuration { get; init; } = TimeSpan.FromHours(1);
}

/// <summary>Current usage stats for a tenant.</summary>
public sealed record TenantUsageStats(
    int CurrentRequests,
    int MaxRequests,
    TimeSpan WindowDuration);

/// <summary>Thread-safe per-tenant usage tracker.</summary>
internal sealed class TenantUsage
{
    private readonly Lock _lock = new();
    private readonly List<DateTimeOffset> _timestamps = [];

    public int RequestCount
    {
        get { lock (_lock) return _timestamps.Count; }
    }

    public void AddRequest()
    {
        lock (_lock)
        {
            _timestamps.Add(DateTimeOffset.UtcNow);
        }
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
