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
        var nextVersion = WorkflowVersioning.ParseVersionOrThrow(definition.Version);

        lock (_lock)
        {
            if (!_store.TryGetValue(definition.Name, out var versions))
            {
                versions = [];
                _store[definition.Name] = versions;
            }

            var previous = WorkflowVersioning.SelectVersion(versions, "latest");
            if (previous is not null &&
                !string.Equals(previous.Version, definition.Version, StringComparison.Ordinal))
            {
                var previousVersion = WorkflowVersioning.ParseVersionOrThrow(previous.Version);
                var breaking = WorkflowVersioning.DetectBreakingChanges(previous, definition);
                definition.BreakingChanges.Clear();
                foreach (var change in breaking)
                    definition.BreakingChanges.Add(change);

                if (breaking.Count > 0 && nextVersion.Major <= previousVersion.Major)
                {
                    throw new InvalidDataException(
                        $"Breaking changes detected between versions {previous.Version} and {definition.Version}. " +
                        "Increment major version for breaking workflow changes.");
                }
            }
            else
            {
                definition.BreakingChanges.Clear();
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

            var result = WorkflowVersioning.SelectVersion(versions, version);
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
                    WorkflowVersioning.SelectVersion(versions, "latest"))
                .Where(v => v is not null)
                .Select(v => v!)
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
}
