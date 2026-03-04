namespace JD.AI.Core.Providers.ModelSearch;

/// <summary>
/// Runs searches in parallel across all registered model-search providers
/// and merges the results.
/// </summary>
public sealed class ModelSearchAggregator
{
    private readonly IReadOnlyList<IRemoteModelSearch> _providers;

    public ModelSearchAggregator(IEnumerable<IRemoteModelSearch> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<IReadOnlyList<RemoteModelResult>> SearchAllAsync(
        string query, CancellationToken ct = default)
    {
        var tasks = _providers.Select(p => p.SearchAsync(query, ct));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.SelectMany(r => r).ToList();
    }

    public async Task<IReadOnlyList<RemoteModelResult>> SearchProviderAsync(
        string providerName, string query, CancellationToken ct = default)
    {
        var tasks = _providers
            .Where(p => string.Equals(p.ProviderName, providerName, StringComparison.Ordinal))
            .Select(p => p.SearchAsync(query, ct));

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.SelectMany(r => r).ToList();
    }
}
