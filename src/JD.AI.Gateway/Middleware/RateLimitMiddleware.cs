using System.Globalization;
using JD.AI.Core.Security;

namespace JD.AI.Gateway.Middleware;

/// <summary>
/// Enforces per-identity (or per-IP) rate limiting with standard rate limit headers.
/// </summary>
public sealed class RateLimitMiddleware(RequestDelegate next, IRateLimiter rateLimiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, "/", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        // Key on identity ID if authenticated, otherwise IP address
        var key = context.Items.TryGetValue("Identity", out var obj) && obj is GatewayIdentity identity
            ? identity.Id
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var result = await rateLimiter.CheckAsync(key, context.RequestAborted);

        // Always set rate limit headers
        var headers = context.Response.Headers;
        headers["X-RateLimit-Limit"] = result.Limit.ToString(CultureInfo.InvariantCulture);
        headers["X-RateLimit-Remaining"] = result.Remaining.ToString(CultureInfo.InvariantCulture);
        headers["X-RateLimit-Reset"] = result.ResetsAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        if (!result.Allowed)
        {
            var retryAfter = Math.Max(1, (int)(result.ResetsAt - DateTimeOffset.UtcNow).TotalSeconds);
            headers["Retry-After"] = retryAfter.ToString(CultureInfo.InvariantCulture);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(
                new { error = "Too Many Requests" },
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
