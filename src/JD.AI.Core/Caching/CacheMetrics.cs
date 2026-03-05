using System.Diagnostics.Metrics;

namespace JD.AI.Core.Caching;

/// <summary>
/// OpenTelemetry metrics for the caching layer.
/// </summary>
public static class CacheMetrics
{
    public const string MeterName = "JD.AI.Cache";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>Total cache hits.</summary>
    public static readonly Counter<long> CacheHits =
        Meter.CreateCounter<long>("jdai.cache.hits", "hits", "Number of cache hits");

    /// <summary>Total cache misses.</summary>
    public static readonly Counter<long> CacheMisses =
        Meter.CreateCounter<long>("jdai.cache.misses", "misses", "Number of cache misses");

    /// <summary>Total cache evictions.</summary>
    public static readonly Counter<long> CacheEvictions =
        Meter.CreateCounter<long>("jdai.cache.evictions", "evictions", "Number of cache evictions");

    /// <summary>Current number of cached entries.</summary>
    public static readonly UpDownCounter<long> CacheSize =
        Meter.CreateUpDownCounter<long>("jdai.cache.size", "entries", "Current cache entry count");
}
