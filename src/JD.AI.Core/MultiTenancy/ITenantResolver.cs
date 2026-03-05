namespace JD.AI.Core.MultiTenancy;

/// <summary>
/// Resolves the current tenant from request context (header, token claim, API key, etc.).
/// Implementations are used by middleware to populate <see cref="TenantContext"/>.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Attempts to resolve a tenant from the given request headers/claims.
    /// Returns <c>null</c> if no tenant can be determined.
    /// </summary>
    Task<TenantInfo?> ResolveAsync(TenantResolutionContext context, CancellationToken ct = default);
}

/// <summary>
/// Resolved tenant information.
/// </summary>
public sealed class TenantInfo
{
    public required string TenantId { get; init; }
    public string? TenantName { get; init; }
}

/// <summary>
/// Input context for tenant resolution, abstracted from HTTP specifics
/// so the resolver can be used in non-HTTP contexts (CLI, daemon).
/// </summary>
public sealed class TenantResolutionContext
{
    /// <summary>Request headers (case-insensitive key lookup).</summary>
    public IDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Authenticated user identity, if available.</summary>
    public string? UserId { get; init; }

    /// <summary>API key used for authentication, if available.</summary>
    public string? ApiKey { get; init; }
}
