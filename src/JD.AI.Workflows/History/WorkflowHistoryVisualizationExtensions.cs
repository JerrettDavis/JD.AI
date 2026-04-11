using System.Text;

namespace JD.AI.Workflows.History;

/// <summary>
/// Extension methods for rendering <see cref="WorkflowHistoryGraph"/> as Mermaid diagrams.
/// </summary>
public static class WorkflowHistoryVisualizationExtensions
{
    /// <summary>
    /// Renders the full history graph as a Mermaid graph TD diagram.
    /// Nodes are deduplicated by fingerprint. Edge labels show weight.
    /// Node color: green for success rate &gt;= 90%, yellow for 50-90%, red for &lt; 50%.
    /// </summary>
    /// <param name="graph">The graph to render.</param>
    /// <param name="maxNodes">Maximum number of nodes to include; trims to top-N by execution count.</param>
    /// <param name="minEdgeWeight">Minimum edge weight required to include an edge.</param>
    public static string ToMermaid(this WorkflowHistoryGraph graph, int maxNodes = 50, long minEdgeWeight = 1)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        // Select top nodes by execution count if over limit
        var nodes = graph.NodeCount <= maxNodes
            ? graph.Nodes
            : (IEnumerable<WorkflowHistoryNode>)graph.Nodes.OrderByDescending(n => n.ExecutionCount).Take(maxNodes);

        var includedFingerprints = nodes.Select(n => n.Fingerprint).ToHashSet(StringComparer.Ordinal);

        // Emit node shapes
        foreach (var node in nodes)
        {
            var nodeId = MakeNodeId(node.Fingerprint);
            var label = EscapeLabel(node.Name);
            var shape = node.Kind switch
            {
                AgentStepKind.Conditional => $"{nodeId}{{{{{label}}}}}",
                AgentStepKind.Loop => $"{nodeId}[/{label}\\]",
                AgentStepKind.Nested => $"{nodeId}[[{label}]]",
                _ => $"{nodeId}[\"{label}\"]",
            };
            sb.AppendLine($"    {shape}");
        }

        // Emit edges (only between included nodes, weight >= minEdgeWeight)
        foreach (var edge in graph.Edges.Where(e =>
            e.Weight >= minEdgeWeight &&
            includedFingerprints.Contains(e.SourceFingerprint) &&
            includedFingerprints.Contains(e.TargetFingerprint)))
        {
            var src = MakeNodeId(edge.SourceFingerprint);
            var tgt = MakeNodeId(edge.TargetFingerprint);
            var label = $"w:{edge.Weight}";
            if (edge.Kind == EdgeKind.SubStep)
                sb.AppendLine($"    {src} -. \"{label}\" .-> {tgt}");
            else
                sb.AppendLine($"    {src} -- \"{label}\" --> {tgt}");
        }

        // Emit style directives based on success rate
        foreach (var node in nodes)
        {
            var nodeId = MakeNodeId(node.Fingerprint);
            var style = node.SuccessRate >= 0.9
                ? $"style {nodeId} fill:#2d6a2d,color:#fff"
                : node.SuccessRate >= 0.5
                    ? $"style {nodeId} fill:#c8a600,color:#000"
                    : $"style {nodeId} fill:#8b1a1a,color:#fff";
            sb.AppendLine($"    {style}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a subgraph rooted at the given fingerprint using BFS up to <paramref name="maxDepth"/> levels.
    /// </summary>
    /// <param name="graph">The graph to render.</param>
    /// <param name="rootFingerprint">The fingerprint of the root node.</param>
    /// <param name="maxDepth">Maximum BFS depth from the root.</param>
    public static string ToMermaid(this WorkflowHistoryGraph graph, string rootFingerprint, int maxDepth = 5)
    {
        // BFS-collect node fingerprints up to maxDepth
        var included = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<(string fingerprint, int depth)>();
        queue.Enqueue((rootFingerprint, 0));
        included.Add(rootFingerprint);

        while (queue.Count > 0)
        {
            var (fp, depth) = queue.Dequeue();
            if (depth >= maxDepth)
                continue;
            foreach (var edge in graph.GetOutgoingEdges(fp))
            {
                if (included.Add(edge.TargetFingerprint))
                    queue.Enqueue((edge.TargetFingerprint, depth + 1));
            }
        }

        var filtered = new FilteredWorkflowHistoryGraph(graph, included);
        return filtered.ToMermaid();
    }

    // Prefix fingerprint with "N_" so the ID is always a valid Mermaid identifier.
    private static string MakeNodeId(string fingerprint) => $"N_{fingerprint}";

    private static string EscapeLabel(string name) =>
        name.Replace("\"", "'")
            .Replace("[", "(")
            .Replace("]", ")")
            .Replace("{", "(")
            .Replace("}", ")");
}

/// <summary>
/// Thin projection of a <see cref="WorkflowHistoryGraph"/> to a subset of its nodes.
/// Used internally by the subgraph overload of <see cref="WorkflowHistoryVisualizationExtensions.ToMermaid"/>.
/// </summary>
internal sealed class FilteredWorkflowHistoryGraph
{
    private readonly WorkflowHistoryGraph _source;
    private readonly HashSet<string> _included;

    public FilteredWorkflowHistoryGraph(WorkflowHistoryGraph source, HashSet<string> included)
    {
        _source = source;
        _included = included;
    }

    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");

        foreach (var node in _source.Nodes.Where(n => _included.Contains(n.Fingerprint)))
        {
            var nodeId = $"N_{node.Fingerprint}";
            var label = node.Name
                .Replace("\"", "'")
                .Replace("[", "(")
                .Replace("]", ")")
                .Replace("{", "(")
                .Replace("}", ")");
            var shape = node.Kind switch
            {
                AgentStepKind.Conditional => $"{nodeId}{{{{{label}}}}}",
                AgentStepKind.Loop => $"{nodeId}[/{label}\\]",
                AgentStepKind.Nested => $"{nodeId}[[{label}]]",
                _ => $"{nodeId}[\"{label}\"]",
            };
            sb.AppendLine($"    {shape}");
        }

        foreach (var edge in _source.Edges.Where(e =>
            _included.Contains(e.SourceFingerprint) &&
            _included.Contains(e.TargetFingerprint)))
        {
            var src = $"N_{edge.SourceFingerprint}";
            var tgt = $"N_{edge.TargetFingerprint}";
            var label = $"w:{edge.Weight}";
            if (edge.Kind == EdgeKind.SubStep)
                sb.AppendLine($"    {src} -. \"{label}\" .-> {tgt}");
            else
                sb.AppendLine($"    {src} -- \"{label}\" --> {tgt}");
        }

        return sb.ToString();
    }
}
