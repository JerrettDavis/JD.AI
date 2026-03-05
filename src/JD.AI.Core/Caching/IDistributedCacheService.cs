namespace JD.AI.Core.Caching;

/// <summary>
/// Generic distributed cache abstraction. Implementations may use in-memory,
/// Redis, or other backing stores. All keys are string-based; values are
/// serialized to/from byte arrays.
/// </summary>
public interface IDistributedCacheService
{
    /// <summary>Gets a cached value by key. Returns <c>null</c> if not found or expired.</summary>
    Task<byte[]?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Sets a cached value with an optional TTL.</summary>
    Task SetAsync(string key, byte[] value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Removes a cached entry.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Checks if a key exists in the cache.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Convenience extensions for working with string values.
/// </summary>
public static class CacheServiceExtensions
{
    /// <summary>Gets a cached string value.</summary>
    public static async Task<string?> GetStringAsync(
        this IDistributedCacheService cache, string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct).ConfigureAwait(false);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Sets a cached string value.</summary>
    public static Task SetStringAsync(
        this IDistributedCacheService cache, string key, string value,
        TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return cache.SetAsync(key, bytes, ttl, ct);
    }
}
