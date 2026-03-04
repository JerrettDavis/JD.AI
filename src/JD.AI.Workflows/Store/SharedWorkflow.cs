namespace JD.AI.Workflows.Store;

/// <summary>Controls who can see and install a shared workflow.</summary>
public enum WorkflowVisibility { Private, Team, Organization, Public }

/// <summary>
/// A versioned workflow published to a shared workflow store.
/// Enables enterprise teams to share, discover, and install workflows.
/// </summary>
public sealed class SharedWorkflow
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..16];
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Description { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public IList<string> Tags { get; init; } = [];
    public IList<string> RequiredTools { get; init; } = [];
    public WorkflowVisibility Visibility { get; init; } = WorkflowVisibility.Team;
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
    public string DefinitionJson { get; init; } = string.Empty;
}
