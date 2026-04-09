namespace JD.AI.Workflows.History;

public sealed class WorkflowHistoryAdvisor : IWorkflowHistoryAdvisor
{
    private readonly IWorkflowHistoryGraphStore _store;

    public WorkflowHistoryAdvisor(IWorkflowHistoryGraphStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<HistoryAdvisory> AdviseAsync(AgentWorkflowDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var graph = await _store.LoadAsync(ct).ConfigureAwait(false);
        if (graph.NodeCount == 0)
            return new HistoryAdvisory { HasHistory = false };

        var stepAdvisories = new List<StepAdvisory>();
        var seenCount = 0;

        foreach (var step in FlattenSteps(definition.Steps))
        {
            var fp = StepFingerprint.Compute(step);
            var node = graph.GetNode(fp);
            var outgoing = graph.GetOutgoingEdges(fp);
            var mostCommonNext = outgoing
                .Where(e => e.Kind == EdgeKind.Sequential)
                .OrderByDescending(e => e.Weight)
                .Select(e => graph.GetNode(e.TargetFingerprint)?.Name)
                .FirstOrDefault();

            var previouslySeen = node is not null;
            if (previouslySeen) seenCount++;

            stepAdvisories.Add(new StepAdvisory
            {
                StepName = step.Name,
                Fingerprint = fp,
                PreviouslySeen = previouslySeen,
                HistoricalSuccessRate = node?.SuccessRate ?? 0.0,
                HistoricalAverageDuration = node?.AverageDuration ?? TimeSpan.Zero,
                MostCommonNextStep = mostCommonNext,
            });
        }

        var totalSteps = stepAdvisories.Count;
        var familiarityScore = totalSteps > 0 ? (double)seenCount / totalSteps : 0.0;
        var similarPaths = await FindSimilarPathsAsync(definition, topN: 3, ct: ct).ConfigureAwait(false);

        return new HistoryAdvisory
        {
            HasHistory = seenCount > 0,
            StepAdvisories = stepAdvisories,
            SimilarPaths = similarPaths,
            FamiliarityScore = familiarityScore,
        };
    }

    public async Task<IReadOnlyList<WorkflowPathSummary>> FindWorkflowsThroughStepAsync(
        string stepNameOrFingerprint, int limit = 20, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepNameOrFingerprint);

        var graph = await _store.LoadAsync(ct).ConfigureAwait(false);

        var fp = ResolveFingerprint(graph, stepNameOrFingerprint);
        if (fp is null) return [];

        var allEdges = graph.GetOutgoingEdges(fp).Concat(graph.GetIncomingEdges(fp));
        var workflowNames = allEdges.SelectMany(e => e.WorkflowNames).Distinct().ToList();

        return workflowNames
            .Select(name =>
            {
                var wfNodes = graph.Nodes.Where(n => n.WorkflowNames.Contains(name)).ToList();
                return new WorkflowPathSummary
                {
                    WorkflowName = name,
                    StepCount = wfNodes.Count,
                    ExecutionCount = wfNodes.Sum(n => n.ExecutionCount),
                    SuccessRate = wfNodes.Count > 0 ? wfNodes.Average(n => n.SuccessRate) : 0.0,
                };
            })
            .OrderByDescending(s => s.ExecutionCount)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<WeightedPath>> GetCommonPathsFromAsync(
        string stepNameOrFingerprint, int topN = 5, int maxDepth = 10, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepNameOrFingerprint);

        var graph = await _store.LoadAsync(ct).ConfigureAwait(false);

        var startFp = ResolveFingerprint(graph, stepNameOrFingerprint);
        if (startFp is null) return [];

        var paths = new List<WeightedPath>();

        for (var i = 0; i < topN; i++)
        {
            var path = new List<WorkflowHistoryNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = startFp;
            long minWeight = long.MaxValue;

            while (path.Count < maxDepth && !visited.Contains(current))
            {
                var node = graph.GetNode(current);
                if (node is null) break;
                path.Add(node);
                visited.Add(current);

                var next = graph.GetOutgoingEdges(current)
                    .Where(e => e.Kind == EdgeKind.Sequential && !visited.Contains(e.TargetFingerprint))
                    .OrderByDescending(e => e.Weight)
                    .Skip(i) // alternate paths on subsequent iterations
                    .FirstOrDefault();

                if (next is null) break;
                minWeight = Math.Min(minWeight, next.Weight);
                current = next.TargetFingerprint;
            }

            if (path.Count > 0)
            {
                paths.Add(new WeightedPath
                {
                    Steps = path,
                    TotalWeight = minWeight == long.MaxValue ? 0 : minWeight,
                    AverageSuccessRate = path.Average(n => n.SuccessRate),
                });
            }
        }

        return paths
            .DistinctBy(p => string.Join(",", p.Steps.Select(s => s.Fingerprint)))
            .ToList();
    }

    public async Task<IReadOnlyList<SimilarPathResult>> FindSimilarPathsAsync(
        AgentWorkflowDefinition definition, int topN = 5, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var graph = await _store.LoadAsync(ct).ConfigureAwait(false);
        if (graph.NodeCount == 0) return [];

        var candidateFingerprints = FlattenSteps(definition.Steps)
            .Select(StepFingerprint.Compute)
            .ToHashSet(StringComparer.Ordinal);

        if (candidateFingerprints.Count == 0) return [];

        var allWorkflows = graph.Nodes
            .SelectMany(n => n.WorkflowNames)
            .Distinct()
            .ToList();

        var results = new List<SimilarPathResult>();

        foreach (var wfName in allWorkflows)
        {
            var wfFingerprints = graph.Nodes
                .Where(n => n.WorkflowNames.Contains(wfName))
                .Select(n => n.Fingerprint)
                .ToHashSet(StringComparer.Ordinal);

            var intersection = candidateFingerprints.Intersect(wfFingerprints).ToList();
            var union = candidateFingerprints.Union(wfFingerprints).ToList();
            var jaccard = union.Count > 0 ? (double)intersection.Count / union.Count : 0.0;

            if (jaccard <= 0) continue;

            var uniqueToCandidate = candidateFingerprints
                .Except(wfFingerprints)
                .Select(fp => graph.GetNode(fp)?.Name ?? fp)
                .ToList();

            var sharedNames = intersection
                .Select(fp => graph.GetNode(fp)?.Name ?? fp)
                .ToList();

            var wfNodes = graph.Nodes.Where(n => n.WorkflowNames.Contains(wfName)).ToList();

            results.Add(new SimilarPathResult
            {
                Similarity = jaccard,
                WorkflowName = wfName,
                SharedStepNames = sharedNames,
                UniqueToCandidate = uniqueToCandidate,
                TotalExecutions = wfNodes.Sum(n => n.ExecutionCount),
                SuccessRate = wfNodes.Count > 0 ? wfNodes.Average(n => n.SuccessRate) : 0.0,
            });
        }

        return results.OrderByDescending(r => r.Similarity).Take(topN).ToList();
    }

    private static string? ResolveFingerprint(WorkflowHistoryGraph graph, string stepNameOrFingerprint)
    {
        // Check if it looks like a hex fingerprint (16 chars, all hex digits)
        if (stepNameOrFingerprint.Length == 16 &&
            stepNameOrFingerprint.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F')))
        {
            return graph.GetNode(stepNameOrFingerprint) is not null ? stepNameOrFingerprint : null;
        }

        return graph.Nodes.FirstOrDefault(n =>
            n.Name.Equals(stepNameOrFingerprint, StringComparison.OrdinalIgnoreCase))?.Fingerprint;
    }

    private static List<AgentStepDefinition> FlattenSteps(IEnumerable<AgentStepDefinition> steps)
    {
        var result = new List<AgentStepDefinition>();
        foreach (var step in steps)
        {
            result.Add(step);
            if (step.SubSteps is { Count: > 0 })
                result.AddRange(FlattenSteps(step.SubSteps));
        }
        return result;
    }
}
