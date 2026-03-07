namespace JD.AI.Workflows;

/// <summary>Detects whether an agent request should be routed through a workflow pipeline.</summary>
public interface IAgentWorkflowDetector
{
    bool IsWorkflowRequired(AgentRequest request);
}

/// <summary>Persists and retrieves versioned workflow definitions.</summary>
public interface IWorkflowCatalog
{
    Task SaveAsync(AgentWorkflowDefinition definition, CancellationToken ct = default);
    Task<AgentWorkflowDefinition?> GetAsync(string name, string? version = null, CancellationToken ct = default);
    Task<IReadOnlyList<AgentWorkflowDefinition>> ListAsync(CancellationToken ct = default);
    Task<bool> DeleteAsync(string name, string? version = null, CancellationToken ct = default);
}

/// <summary>Matches incoming requests against catalogued workflows for reuse.</summary>
public interface IWorkflowMatcher
{
    Task<WorkflowMatchResult?> MatchAsync(AgentRequest request, CancellationToken ct = default);
}

/// <summary>Emits workflow definitions in various DSL formats.</summary>
public interface IWorkflowEmitter
{
    WorkflowArtifact Emit(AgentWorkflowDefinition definition, WorkflowExportFormat format = WorkflowExportFormat.Json);
}
