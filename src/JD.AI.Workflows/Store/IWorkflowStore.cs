namespace JD.AI.Workflows.Store;

/// <summary>
/// A shared workflow store that enables teams to publish, discover, and install workflows.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>Publishes a workflow to the shared store.</summary>
    Task PublishAsync(SharedWorkflow workflow, CancellationToken ct = default);

    /// <summary>Gets a specific workflow by name or ID, optionally at a specific version.</summary>
    Task<SharedWorkflow?> GetAsync(string nameOrId, string? version = null, CancellationToken ct = default);

    /// <summary>Lists all workflows in the catalog, optionally filtered by tag or author.</summary>
    Task<IReadOnlyList<SharedWorkflow>> CatalogAsync(string? tag = null, string? author = null, CancellationToken ct = default);

    /// <summary>Searches workflows by substring match on name, description, and tags.</summary>
    Task<IReadOnlyList<SharedWorkflow>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>Lists all versions of a named workflow.</summary>
    Task<IReadOnlyList<SharedWorkflow>> VersionsAsync(string name, CancellationToken ct = default);

    /// <summary>Installs a workflow from the store to a local directory.</summary>
    Task<bool> InstallAsync(string nameOrId, string? version, string localDirectory, CancellationToken ct = default);
}
