using System.Globalization;
using JD.AI.Core.Infrastructure;
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

        if (ShouldBypassRateLimit(path))
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

    private static bool ShouldBypassRateLimit(string path)
    {
        if (string.Equals(path, "/", StringComparison.Ordinal))
            return true;

        if (string.Equals(path, GatewayRuntimeDefaults.HealthPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, GatewayRuntimeDefaults.HealthReadyPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, GatewayRuntimeDefaults.HealthLivePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, GatewayRuntimeDefaults.HealthStartupPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, GatewayRuntimeDefaults.ReadyPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HasPathSegmentPrefix(path, "/_framework") ||
            HasPathSegmentPrefix(path, "/_content") ||
            HasPathSegmentPrefix(path, "/css") ||
            HasPathSegmentPrefix(path, "/js"))
        {
            return true;
        }

        return string.Equals(path, "/index.html", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/appsettings.json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/favicon.png", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/icon-192.png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPathSegmentPrefix(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        (path.Length == prefix.Length || path[prefix.Length] == '/');
}
