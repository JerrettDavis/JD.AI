namespace JD.AI.Workflows.History;

public interface IWorkflowHistoryGraphStore
{
    Task<WorkflowHistoryGraph> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(WorkflowHistoryGraph graph, CancellationToken ct = default);
    Task UpsertNodeAsync(WorkflowHistoryNode node, CancellationToken ct = default);
    Task UpsertEdgeAsync(WorkflowHistoryEdge edge, CancellationToken ct = default);
    Task<WorkflowHistoryNode?> GetNodeAsync(string fingerprint, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowHistoryNode>> SearchNodesAsync(string query, int limit = 20, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowHistoryEdge>> GetTopEdgesFromAsync(string fingerprint, int topN = 10, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowHistoryEdge>> GetEdgesForWorkflowAsync(string workflowName, CancellationToken ct = default);
}
