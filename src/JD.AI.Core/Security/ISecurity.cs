namespace JD.AI.Core.Security;

/// <summary>
/// Represents an authenticated identity (API key, OAuth token, etc.).
/// </summary>
public record GatewayIdentity(
    string Id,
    string DisplayName,
    GatewayRole Role,
    DateTimeOffset AuthenticatedAt)
{
    public IReadOnlyDictionary<string, string> Claims { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Role hierarchy for access control.
/// </summary>
public enum GatewayRole
{
    Guest = 0,
    User = 10,
    Operator = 50,
    Admin = 100
}

/// <summary>
/// Authenticates requests to the gateway.
/// </summary>
public interface IAuthProvider
{
    /// <summary>Validates a credential and returns the identity.</summary>
    Task<GatewayIdentity?> AuthenticateAsync(string credential, CancellationToken ct = default);
}

/// <summary>
/// Simple API key authentication provider.
/// </summary>
public sealed class ApiKeyAuthProvider : IAuthProvider
{
    private readonly Dictionary<string, GatewayIdentity> _keys = new(StringComparer.Ordinal);

    public void RegisterKey(string apiKey, string name, GatewayRole role = GatewayRole.User)
    {
        _keys[apiKey] = new GatewayIdentity(
            Guid.NewGuid().ToString("N")[..12],
            name,
            role,
            DateTimeOffset.UtcNow);
    }

    public Task<GatewayIdentity?> AuthenticateAsync(string credential, CancellationToken ct = default)
    {
        _keys.TryGetValue(credential, out var identity);
        return Task.FromResult(identity);
    }
}

/// <summary>
/// Rate limiter for gateway operations.
/// </summary>
public interface IRateLimiter
{
    /// <summary>Returns true if the request is allowed; false if rate-limited.</summary>
    Task<bool> AllowAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Simple sliding window rate limiter.
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _windows = [];
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly Lock _lock = new();

    public SlidingWindowRateLimiter(int maxRequests = 60, TimeSpan? window = null)
    {
        _maxRequests = maxRequests;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public Task<bool> AllowAsync(string key, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_windows.TryGetValue(key, out var queue))
            {
                queue = new Queue<DateTimeOffset>();
                _windows[key] = queue;
            }

            // Trim expired entries
            while (queue.Count > 0 && now - queue.Peek() >= _window)
                queue.Dequeue();

            if (queue.Count >= _maxRequests)
                return Task.FromResult(false);

            queue.Enqueue(now);
            return Task.FromResult(true);
        }
    }
}
