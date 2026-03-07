using JD.AI.Core.MultiTenancy;

namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Wraps an underlying <see cref="ICredentialStore"/> and enforces tenant-namespace isolation.
/// All credential keys are automatically prefixed with the current tenant ID so that
/// credentials belonging to different tenants cannot be accessed across tenant boundaries.
/// </summary>
/// <remarks>
/// This store should be registered as a scoped service so that it inherits the current
/// <see cref="TenantContext"/> from the DI scope.
/// </remarks>
public sealed class TenantScopedCredentialStore : ICredentialStore
{
    private readonly ICredentialStore _inner;
    private readonly TenantContext _tenantContext;

    /// <summary>
    /// Initializes a new <see cref="TenantScopedCredentialStore"/>.
    /// </summary>
    /// <param name="inner">The backing credential store (e.g. EncryptedFileStore, Azure KV).</param>
    /// <param name="tenantContext">The current tenant scope.</param>
    public TenantScopedCredentialStore(ICredentialStore inner, TenantContext tenantContext)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    /// <inheritdoc/>
    public bool IsAvailable => _inner.IsAvailable;

    /// <inheritdoc/>
    public string StoreName => $"{_inner.StoreName} (tenant-scoped)";

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        _inner.GetAsync(BuildKey(key), ct);

    /// <inheritdoc/>
    public Task SetAsync(string key, string value, CancellationToken ct = default) =>
        _inner.SetAsync(BuildKey(key), value, ct);

    /// <inheritdoc/>
    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        _inner.RemoveAsync(BuildKey(key), ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var tenantPrefix = BuildKey(prefix ?? string.Empty);
        var innerKeys = await _inner.ListKeysAsync(tenantPrefix, ct).ConfigureAwait(false);

        // Strip the tenant prefix from returned keys so callers see unnamespaced keys
        var tenantPart = GetTenantPrefix();
        return innerKeys
            .Where(k => k.StartsWith(tenantPart, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[tenantPart.Length..])
            .ToList();
    }

    private string BuildKey(string key)
    {
        EnsureTenantResolved();
        return $"{GetTenantPrefix()}{key}";
    }

    private string GetTenantPrefix() => $"tenants/{_tenantContext.TenantId}/";

    private void EnsureTenantResolved()
    {
        if (!_tenantContext.IsResolved)
        {
            throw new InvalidOperationException(
                "TenantScopedCredentialStore requires an active tenant context. " +
                "Ensure a tenant has been resolved before accessing credentials.");
        }
    }
}
