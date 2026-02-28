using JD.AI.Core.Security;

namespace JD.AI.Gateway.Middleware;

/// <summary>
/// Enforces per-identity (or per-IP) rate limiting.
/// </summary>
public sealed class RateLimitMiddleware(RequestDelegate next, IRateLimiter rateLimiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Key on identity ID if authenticated, otherwise IP address
        var key = context.Items.TryGetValue("Identity", out var obj) && obj is GatewayIdentity identity
            ? identity.Id
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!await rateLimiter.AllowAsync(key, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(
                new { error = "Too Many Requests" },
                context.RequestAborted);
            return;
        }

        await next(context);
    }
}
