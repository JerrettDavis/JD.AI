namespace JD.AI.Workflows.History;

public sealed class InMemoryWorkflowHistoryGraphStore : IWorkflowHistoryGraphStore
{
    private WorkflowHistoryGraph _graph = new();

    public Task<WorkflowHistoryGraph> LoadAsync(CancellationToken ct = default) =>
        Task.FromResult(_graph);

    public Task SaveAsync(WorkflowHistoryGraph graph, CancellationToken ct = default)
    {
        _graph = graph;
        return Task.CompletedTask;
    }

    public Task UpsertNodeAsync(WorkflowHistoryNode node, CancellationToken ct = default)
    {
        _graph.GetOrAddNode(node.Fingerprint, () => node);
        return Task.CompletedTask;
    }

    public Task UpsertEdgeAsync(WorkflowHistoryEdge edge, CancellationToken ct = default)
    {
        _graph.RecordTransition(
            edge.SourceFingerprint,
            edge.TargetFingerprint,
            edge.Kind,
            edge.WorkflowNames.FirstOrDefault() ?? string.Empty,
            edge.AverageTransitionTime);
        return Task.CompletedTask;
    }

    public Task<WorkflowHistoryNode?> GetNodeAsync(string fingerprint, CancellationToken ct = default) =>
        Task.FromResult(_graph.GetNode(fingerprint));

    public Task<IReadOnlyList<WorkflowHistoryNode>> SearchNodesAsync(string query, int limit = 20, CancellationToken ct = default)
    {
        IReadOnlyList<WorkflowHistoryNode> results = _graph.Nodes
            .Where(n => n.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<WorkflowHistoryEdge>> GetTopEdgesFromAsync(string fingerprint, int topN = 10, CancellationToken ct = default)
    {
        IReadOnlyList<WorkflowHistoryEdge> results = _graph.GetOutgoingEdges(fingerprint)
            .OrderByDescending(e => e.Weight)
            .Take(topN)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<WorkflowHistoryEdge>> GetEdgesForWorkflowAsync(string workflowName, CancellationToken ct = default)
    {
        IReadOnlyList<WorkflowHistoryEdge> results = _graph.Edges
            .Where(e => e.WorkflowNames.Contains(workflowName))
            .ToList();
        return Task.FromResult(results);
    }
}
