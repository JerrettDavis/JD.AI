using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace JD.AI.Core.Security;

/// <summary>
/// Redis-backed distributed sliding window rate limiter using sorted sets.
/// Falls back to local <see cref="SlidingWindowRateLimiter"/> if Redis is unavailable.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimiter> _logger;
    private readonly SlidingWindowRateLimiter _fallback;
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private const string KeyPrefix = "jdai:ratelimit:";

    public RedisRateLimiter(
        IConnectionMultiplexer redis,
        ILogger<RedisRateLimiter> logger,
        int maxRequests = 60,
        TimeSpan? window = null)
    {
        _redis = redis;
        _logger = logger;
        _maxRequests = maxRequests;
        _window = window ?? TimeSpan.FromMinutes(1);
        _fallback = new SlidingWindowRateLimiter(maxRequests, _window);
    }

    public async Task<bool> AllowAsync(string key, CancellationToken ct = default)
    {
        var result = await CheckAsync(key, ct).ConfigureAwait(false);
        return result.Allowed;
    }

    public async Task<RateLimitResult> CheckAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await CheckRedisAsync(key).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis rate limiter unavailable, falling back to local limiter");
            return await _fallback.CheckAsync(key, ct).ConfigureAwait(false);
        }
    }

    private async Task<RateLimitResult> CheckRedisAsync(string key)
    {
        var db = _redis.GetDatabase();
        var redisKey = new RedisKey(KeyPrefix + key);
        var now = DateTimeOffset.UtcNow;
        var windowStart = now - _window;
        var nowScore = now.ToUnixTimeMilliseconds();
        var windowStartScore = windowStart.ToUnixTimeMilliseconds();

        // Lua script: atomic ZREMRANGEBYSCORE + ZCARD + ZADD + EXPIRE
        var script = @"
            redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
            local count = redis.call('ZCARD', KEYS[1])
            if count < tonumber(ARGV[2]) then
                redis.call('ZADD', KEYS[1], ARGV[3], ARGV[3] .. ':' .. math.random(1000000))
                redis.call('EXPIRE', KEYS[1], ARGV[4])
                return {1, count + 1}
            else
                redis.call('EXPIRE', KEYS[1], ARGV[4])
                return {0, count}
            end
        ";

        var result = (RedisResult[]?)await db.ScriptEvaluateAsync(
            script,
            [redisKey],
            [
                windowStartScore,
                _maxRequests,
                nowScore,
                (int)_window.TotalSeconds + 1,
            ]).ConfigureAwait(false);

        var allowed = (int)result![0] == 1;
        var currentCount = (int)result[1];
        var remaining = Math.Max(0, _maxRequests - currentCount);
        var resetsAt = now + _window;

        return new RateLimitResult(allowed, _maxRequests, remaining, resetsAt);
    }
}
