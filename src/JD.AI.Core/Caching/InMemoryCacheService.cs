using System.Collections.Concurrent;

namespace JD.AI.Core.Caching;

/// <summary>
/// In-process cache implementation using <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for single-node deployments. Entries expire based on TTL.
/// A background sweep periodically removes expired entries.
/// </summary>
public sealed class InMemoryCacheService : IDistributedCacheService, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _sweepTimer;
    private readonly int _maxEntries;
    private bool _disposed;

    /// <param name="maxEntries">Maximum number of cache entries before eviction. Default 10,000.</param>
    /// <param name="sweepInterval">How often to run expiry sweep. Default 60 seconds.</param>
    public InMemoryCacheService(int maxEntries = 10_000, TimeSpan? sweepInterval = null)
    {
        _maxEntries = maxEntries;
        var interval = sweepInterval ?? TimeSpan.FromSeconds(60);
        _sweepTimer = new Timer(_ => Sweep(), null, interval, interval);
    }

    /// <summary>Number of entries currently in the cache.</summary>
    public int Count => _cache.Count;

    public Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt is null || entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                entry.LastAccessed = DateTimeOffset.UtcNow;
                return Task.FromResult<byte[]?>(entry.Value);
            }

            // Expired — remove
            _cache.TryRemove(key, out _);
        }

        return Task.FromResult<byte[]?>(null);
    }

    public Task SetAsync(string key, byte[] value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var entry = new CacheEntry
        {
            Value = value,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessed = DateTimeOffset.UtcNow,
            ExpiresAt = ttl.HasValue ? DateTimeOffset.UtcNow + ttl.Value : null,
        };

        _cache[key] = entry;

        // Evict oldest entries if over capacity
        if (_cache.Count > _maxEntries)
            EvictOldest(_cache.Count - _maxEntries);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt is null || entry.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult(true);

            _cache.TryRemove(key, out _);
        }

        return Task.FromResult(false);
    }

    private void Sweep()
    {
        if (_disposed) return;
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt is not null && kvp.Value.ExpiresAt <= now)
                _cache.TryRemove(kvp.Key, out _);
        }
    }

    private void EvictOldest(int count)
    {
        var oldest = _cache
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(count)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldest)
            _cache.TryRemove(key, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sweepTimer.Dispose();
        _cache.Clear();
    }

    private sealed class CacheEntry
    {
        public required byte[] Value { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset LastAccessed { get; set; }
        public DateTimeOffset? ExpiresAt { get; init; }
    }
}
