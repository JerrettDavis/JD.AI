namespace JD.AI.Core.MultiTenancy;

/// <summary>
/// Resolves tenant from the <c>X-Tenant-Id</c> request header.
/// Falls back to a default tenant when no header is present.
/// </summary>
public sealed class HeaderTenantResolver : ITenantResolver
{
    /// <summary>Standard header name for tenant identification.</summary>
    public const string TenantHeader = "X-Tenant-Id";

    private readonly string? _defaultTenantId;

    /// <param name="defaultTenantId">
    /// Optional default tenant for requests without a tenant header.
    /// When null, unresolved requests have no tenant context.
    /// </param>
    public HeaderTenantResolver(string? defaultTenantId = null)
    {
        _defaultTenantId = defaultTenantId;
    }

    public Task<TenantInfo?> ResolveAsync(TenantResolutionContext context, CancellationToken ct = default)
    {
        if (context.Headers.TryGetValue(TenantHeader, out var tenantId)
            && !string.IsNullOrWhiteSpace(tenantId))
        {
            return Task.FromResult<TenantInfo?>(new TenantInfo
            {
                TenantId = tenantId.Trim(),
            });
        }

        if (_defaultTenantId is not null)
        {
            return Task.FromResult<TenantInfo?>(new TenantInfo
            {
                TenantId = _defaultTenantId,
            });
        }

        return Task.FromResult<TenantInfo?>(null);
    }
}
