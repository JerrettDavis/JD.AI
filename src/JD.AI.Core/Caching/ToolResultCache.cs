using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Caching;

/// <summary>
/// Caches deterministic tool results to avoid redundant invocations.
/// Tools must be explicitly registered as cacheable. Cache keys are derived
/// from tool name + arguments hash.
/// </summary>
public sealed class ToolResultCache
{
    private readonly IDistributedCacheService _cache;
    private readonly ILogger? _logger;
    private readonly HashSet<string> _cacheableTools;
    private readonly TimeSpan _defaultTtl;
    private readonly Dictionary<string, TimeSpan> _toolTtls;

    private long _hits;
    private long _misses;

    /// <param name="cache">Backing cache service.</param>
    /// <param name="cacheableTools">Tool names that are safe to cache (deterministic).</param>
    /// <param name="defaultTtl">Default TTL for cached results. Default 5 minutes.</param>
    /// <param name="toolTtls">Per-tool TTL overrides.</param>
    /// <param name="logger">Optional logger.</param>
    public ToolResultCache(
        IDistributedCacheService cache,
        IEnumerable<string> cacheableTools,
        TimeSpan? defaultTtl = null,
        IDictionary<string, TimeSpan>? toolTtls = null,
        ILogger? logger = null)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheableTools = new HashSet<string>(
            cacheableTools ?? [], StringComparer.OrdinalIgnoreCase);
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        _toolTtls = toolTtls is not null
            ? new Dictionary<string, TimeSpan>(toolTtls, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>Number of cache hits.</summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>Number of cache misses.</summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// Attempts to get a cached result for a tool invocation.
    /// Returns <c>null</c> if the tool is not cacheable or no cached result exists.
    /// </summary>
    public async Task<string?> GetCachedResultAsync(
        string toolName, string arguments, CancellationToken ct = default)
    {
        if (!_cacheableTools.Contains(toolName))
            return null;

        var key = BuildKey(toolName, arguments);
        var result = await _cache.GetStringAsync(key, ct).ConfigureAwait(false);

        if (result is not null)
        {
            Interlocked.Increment(ref _hits);
            _logger?.LogDebug("Cache HIT for tool '{Tool}' (key: {Key})", toolName, key);
        }
        else
        {
            Interlocked.Increment(ref _misses);
            _logger?.LogDebug("Cache MISS for tool '{Tool}' (key: {Key})", toolName, key);
        }

        return result;
    }

    /// <summary>
    /// Stores a tool result in the cache.
    /// </summary>
    public async Task SetResultAsync(
        string toolName, string arguments, string result, CancellationToken ct = default)
    {
        if (!_cacheableTools.Contains(toolName))
            return;

        var key = BuildKey(toolName, arguments);
        var ttl = _toolTtls.TryGetValue(toolName, out var customTtl) ? customTtl : _defaultTtl;

        await _cache.SetStringAsync(key, result, ttl, ct).ConfigureAwait(false);
        _logger?.LogDebug("Cached result for tool '{Tool}' (TTL: {Ttl})", toolName, ttl);
    }

    /// <summary>
    /// Invalidates all cached results for a specific tool.
    /// </summary>
    public async Task InvalidateToolAsync(string toolName, CancellationToken ct = default)
    {
        // For in-memory cache, we'd need to enumerate keys.
        // For now, just log. A proper implementation would use key prefixes.
        _logger?.LogInformation("Invalidation requested for tool '{Tool}'", toolName);
        await Task.CompletedTask;
    }

    /// <summary>Checks if a tool is registered as cacheable.</summary>
    public bool IsCacheable(string toolName) => _cacheableTools.Contains(toolName);

    private static string BuildKey(string toolName, string arguments)
    {
        var input = $"{toolName}:{arguments}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"tool:{toolName}:{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }
}
