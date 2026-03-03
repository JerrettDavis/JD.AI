namespace JD.AI.Workflows;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWorkflowCatalog"/>.
/// Suitable for testing and ephemeral use.
/// </summary>
public sealed class InMemoryWorkflowCatalog : IWorkflowCatalog
{
    private readonly Dictionary<string, List<AgentWorkflowDefinition>> _store =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(definition.Name, out var versions))
            {
                versions = [];
                _store[definition.Name] = versions;
            }

            versions.RemoveAll(w =>
                string.Equals(w.Version, definition.Version, StringComparison.Ordinal));
            versions.Add(definition);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<AgentWorkflowDefinition?> GetAsync(
        string name, string? version = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(name, out var versions))
                return Task.FromResult<AgentWorkflowDefinition?>(null);

            var result = version is not null
                ? versions.FirstOrDefault(w =>
                    string.Equals(w.Version, version, StringComparison.Ordinal))
                : versions.OrderByDescending(w => ParseVersion(w.Version))
                          .FirstOrDefault();

            return Task.FromResult(result);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            var all = _store.Values
                .Select(versions =>
                    versions.OrderByDescending(w => ParseVersion(w.Version)).First())
                .ToList()
                .AsReadOnly();

            return Task.FromResult<IReadOnlyList<AgentWorkflowDefinition>>(all);
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string name, string? version = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(name, out var versions))
                return Task.FromResult(false);

            if (version is not null)
            {
                var removed = versions.RemoveAll(w =>
                    string.Equals(w.Version, version, StringComparison.Ordinal));
                return Task.FromResult(removed > 0);
            }

            _store.Remove(name);
            return Task.FromResult(true);
        }
    }

    private static Version ParseVersion(string versionString) =>
        Version.TryParse(versionString, out var v) ? v : new Version(0, 0);
}
