namespace JD.AI.Core.Providers.Credentials;

/// <summary>
/// Chains multiple <see cref="ICredentialStore"/> instances, trying each in order
/// for reads and writing to the first writable store. Enables layered secret
/// resolution (e.g., Vault → env vars → encrypted file).
/// </summary>
public sealed class ChainedCredentialStore : ICredentialStore
{
    private readonly List<ICredentialStore> _stores;

    public ChainedCredentialStore(IEnumerable<ICredentialStore> stores)
    {
        _stores = stores.Where(s => s.IsAvailable).ToList();
    }

    public ChainedCredentialStore(params ICredentialStore[] stores)
        : this((IEnumerable<ICredentialStore>)stores)
    {
    }

    public bool IsAvailable => _stores.Count > 0;

    public string StoreName =>
        $"Chained[{string.Join(" → ", _stores.Select(s => s.StoreName))}]";

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        foreach (var store in _stores)
        {
            var value = await store.GetAsync(key, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        // Write to the first store (primary)
        return _stores.Count > 0
            ? _stores[0].SetAsync(key, value, ct)
            : Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        // Remove from the first store (primary)
        return _stores.Count > 0
            ? _stores[0].RemoveAsync(key, ct)
            : Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
    {
        var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var store in _stores)
        {
            var keys = await store.ListKeysAsync(prefix, ct).ConfigureAwait(false);
            foreach (var key in keys)
                allKeys.Add(key);
        }

        return allKeys.ToList();
    }
}
