using JD.AI.Core.Infrastructure;
using JD.AI.Core.Security;

namespace JD.AI.Gateway.Middleware;

/// <summary>
/// Authenticates requests via X-API-Key header or api_key query parameter (for SignalR).
/// </summary>
public sealed class ApiKeyAuthMiddleware(RequestDelegate next, IAuthProvider authProvider)
{
    private static readonly string[] SkipPrefixes =
    [
        GatewayRuntimeDefaults.HealthPath,
        GatewayRuntimeDefaults.ReadyPath,
        "/hubs/",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (ShouldSkip(path))
        {
            await next(context);
            return;
        }

        // Try header first, then query param (SignalR WebSocket connections)
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault()
                     ?? context.Request.Query["api_key"].FirstOrDefault();

        if (!string.IsNullOrEmpty(apiKey))
        {
            var identity = await authProvider.AuthenticateAsync(apiKey, context.RequestAborted);
            if (identity is not null)
            {
                context.Items["Identity"] = identity;
                await next(context);
                return;
            }
        }

        // Only enforce auth on /api/* paths
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" }, context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool ShouldSkip(string path)
    {
        foreach (var prefix in SkipPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
