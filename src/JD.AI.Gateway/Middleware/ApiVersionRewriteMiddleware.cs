namespace JD.AI.Gateway.Middleware;

/// <summary>
/// Rewrites unversioned <c>/api/{resource}</c> requests to <c>/api/v1/{resource}</c>
/// for backward compatibility during the API versioning migration.
/// Adds a <c>Sunset</c> header on rewritten requests to signal deprecation.
/// </summary>
public sealed class ApiVersionRewriteMiddleware(RequestDelegate next)
{
    private const string LegacyPrefix = "/api/";
    private const string VersionedPrefix = "/api/v1/";

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (path is not null &&
            path.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith(VersionedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Rewrite to versioned path
            var newPath = string.Concat(VersionedPrefix, path.AsSpan(LegacyPrefix.Length));
            context.Request.Path = newPath;

            // Signal that unversioned paths are deprecated
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["Sunset"] = "2026-09-01T00:00:00Z";
                context.Response.Headers["Deprecation"] = "true";
                context.Response.Headers["Link"] =
                    $"<{VersionedPrefix}{path.AsSpan(LegacyPrefix.Length)}>; rel=\"successor-version\"";
                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
