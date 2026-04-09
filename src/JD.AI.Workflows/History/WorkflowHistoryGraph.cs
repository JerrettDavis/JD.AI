namespace JD.AI.Workflows.History;

public sealed class WorkflowHistoryGraph
{
    private readonly Dictionary<string, WorkflowHistoryNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Source, string Target, EdgeKind Kind), WorkflowHistoryEdge> _edges = [];

    public IReadOnlyCollection<WorkflowHistoryNode> Nodes => _nodes.Values;
    public IReadOnlyCollection<WorkflowHistoryEdge> Edges => _edges.Values;
    public int NodeCount => _nodes.Count;
    public int EdgeCount => _edges.Count;

    public WorkflowHistoryNode? GetNode(string fingerprint) =>
        _nodes.GetValueOrDefault(fingerprint);

    public WorkflowHistoryNode GetOrAddNode(string fingerprint, Func<WorkflowHistoryNode> factory)
    {
        if (_nodes.TryGetValue(fingerprint, out var existing))
            return existing;
        var node = factory();
        _nodes[fingerprint] = node;
        return node;
    }

    public WorkflowHistoryEdge RecordTransition(
        string sourceFingerprint,
        string targetFingerprint,
        EdgeKind kind,
        string workflowName,
        TimeSpan transitionTime)
    {
        var key = (sourceFingerprint, targetFingerprint, kind);
        if (_edges.TryGetValue(key, out var edge))
        {
            edge.Weight++;
            edge.LastSeenAt = DateTimeOffset.UtcNow;
            edge.WorkflowNames.Add(workflowName);
            edge.AverageTransitionTime = TimeSpan.FromTicks(
                (edge.AverageTransitionTime.Ticks * (edge.Weight - 1) + transitionTime.Ticks) / edge.Weight);
            return edge;
        }

        var newEdge = new WorkflowHistoryEdge
        {
            SourceFingerprint = sourceFingerprint,
            TargetFingerprint = targetFingerprint,
            Kind = kind,
            Weight = 1,
            AverageTransitionTime = transitionTime,
            WorkflowNames = new HashSet<string>(StringComparer.Ordinal) { workflowName },
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        };
        _edges[key] = newEdge;
        return newEdge;
    }

    public IReadOnlyList<WorkflowHistoryEdge> GetOutgoingEdges(string fingerprint) =>
        _edges.Values.Where(e => string.Equals(e.SourceFingerprint, fingerprint)).ToList();

    public IReadOnlyList<WorkflowHistoryEdge> GetIncomingEdges(string fingerprint) =>
        _edges.Values.Where(e => string.Equals(e.TargetFingerprint, fingerprint)).ToList();

    public IReadOnlyList<WorkflowHistoryNode> GetReachable(string fingerprint)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(fingerprint);
        visited.Add(fingerprint);
        var result = new List<WorkflowHistoryNode>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (_nodes.TryGetValue(current, out var node) && !string.Equals(current, fingerprint))
                result.Add(node);
            foreach (var e in GetOutgoingEdges(current))
                if (visited.Add(e.TargetFingerprint))
                    queue.Enqueue(e.TargetFingerprint);
        }
        return result;
    }
}
