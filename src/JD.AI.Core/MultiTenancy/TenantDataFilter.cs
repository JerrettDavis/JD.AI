namespace JD.AI.Core.MultiTenancy;

/// <summary>
/// Provides tenant-scoped data filtering. Services use this to ensure
/// queries only return data belonging to the current tenant.
/// </summary>
public sealed class TenantDataFilter
{
    private readonly TenantContext _context;

    public TenantDataFilter(TenantContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Current tenant ID, or null if no tenant is resolved.</summary>
    public string? CurrentTenantId => _context.TenantId;

    /// <summary>Whether multi-tenant filtering is active.</summary>
    public bool IsActive => _context.IsResolved;

    /// <summary>
    /// Filters a collection to only include items belonging to the current tenant.
    /// Items with a null tenant ID are included (shared/global items).
    /// </summary>
    public IEnumerable<T> Filter<T>(IEnumerable<T> items, Func<T, string?> tenantIdSelector)
    {
        if (!IsActive)
            return items; // No tenant context — return all

        return items.Where(item =>
        {
            var itemTenant = tenantIdSelector(item);
            return itemTenant is null // Shared/global item
                   || string.Equals(itemTenant, _context.TenantId, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Checks if an item belongs to the current tenant (or is shared).
    /// </summary>
    public bool BelongsToCurrentTenant(string? itemTenantId)
    {
        if (!IsActive) return true; // No filtering active
        if (itemTenantId is null) return true; // Shared item

        return string.Equals(itemTenantId, _context.TenantId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the current tenant ID to stamp on new data entities.
    /// Returns null if no tenant context is active.
    /// </summary>
    public string? GetTenantStamp() => _context.TenantId;
}
