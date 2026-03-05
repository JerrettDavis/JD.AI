namespace JD.AI.Core.MultiTenancy;

/// <summary>
/// Holds the current tenant identity for the request scope.
/// Flows through DI as a scoped service.
/// </summary>
public sealed class TenantContext
{
    /// <summary>Unique tenant identifier.</summary>
    public string? TenantId { get; set; }

    /// <summary>Human-readable tenant/organization name.</summary>
    public string? TenantName { get; set; }

    /// <summary>Whether a valid tenant has been resolved for this request.</summary>
    public bool IsResolved => !string.IsNullOrWhiteSpace(TenantId);
}
