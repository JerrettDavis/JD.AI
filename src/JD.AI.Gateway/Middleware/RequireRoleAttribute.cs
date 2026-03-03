using JD.AI.Core.Security;

namespace JD.AI.Gateway.Middleware;

/// <summary>
/// Marks an endpoint as requiring a minimum <see cref="GatewayRole"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireRoleAttribute(GatewayRole minimumRole) : Attribute
{
    public GatewayRole MinimumRole { get; } = minimumRole;
}

/// <summary>
/// Endpoint filter that enforces <see cref="RequireRoleAttribute"/>.
/// </summary>
public sealed class RequireRoleFilter(GatewayRole minimumRole) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (context.HttpContext.Items.TryGetValue("Identity", out var obj) &&
            obj is GatewayIdentity identity &&
            identity.Role >= minimumRole)
        {
            return await next(context);
        }

        return Results.Json(
            new { error = "Forbidden" },
            statusCode: StatusCodes.Status403Forbidden);
    }
}

/// <summary>
/// Extension methods for applying role-based access control to endpoints.
/// </summary>
public static class RequireRoleExtensions
{
    public static TBuilder RequireRole<TBuilder>(this TBuilder builder, GatewayRole minimumRole)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new RequireRoleFilter(minimumRole));
        return builder;
    }
}
