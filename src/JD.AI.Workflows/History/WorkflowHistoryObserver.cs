namespace JD.AI.Workflows.History;

/// <summary>
/// Observes workflow executions and ingests them into the workflow history graph.
/// Handles flattening of nested steps, computing fingerprints, and recording
/// transitions (both sequential and sub-step relationships).
/// </summary>
public sealed class WorkflowHistoryObserver : IWorkflowHistoryObserver
{
    private readonly IWorkflowHistoryGraphStore _store;

    public WorkflowHistoryObserver(IWorkflowHistoryGraphStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc/>
    public async Task IngestRunAsync(
        AgentWorkflowDefinition definition,
        WorkflowBridgeResult result,
        CancellationToken ct = default)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition));
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        var graph = await _store.LoadAsync(ct);
        var flatSteps = FlattenSteps(definition.Steps);
        var now = DateTimeOffset.UtcNow;

        // Upsert nodes for all flattened steps
        foreach (var step in flatSteps)
        {
            var fp = StepFingerprint.Compute(step);
            var node = graph.GetOrAddNode(fp, () => new WorkflowHistoryNode
            {
                Fingerprint = fp,
                Name = step.Name,
                Kind = step.Kind,
                Target = step.Target,
                FirstSeenAt = now,
                LastSeenAt = now,
                WorkflowNames = new HashSet<string>(StringComparer.Ordinal) { definition.Name },
            });

            node.ExecutionCount++;
            if (result.Success)
                node.SuccessCount++;
            else
                node.FailureCount++;
            node.LastSeenAt = now;
            node.WorkflowNames.Add(definition.Name);
        }

        // Record sequential edges between consecutive steps
        for (var i = 0; i < flatSteps.Count - 1; i++)
        {
            var src = StepFingerprint.Compute(flatSteps[i]);
            var tgt = StepFingerprint.Compute(flatSteps[i + 1]);
            graph.RecordTransition(src, tgt, EdgeKind.Sequential, definition.Name, TimeSpan.Zero);
        }

        // Record sub-step edges for nested steps
        RecordSubStepEdges(graph, definition.Steps, definition.Name);

        await _store.SaveAsync(graph, ct);
    }

    /// <summary>
    /// Recursively flatten all steps, including sub-steps from nested/loop/conditional steps.
    /// </summary>
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

    /// <summary>
    /// Recursively record SubStep edges between parent steps and their immediate children,
    /// then recurse into children for further nesting.
    /// </summary>
    private static void RecordSubStepEdges(
        WorkflowHistoryGraph graph,
        IEnumerable<AgentStepDefinition> steps,
        string workflowName)
    {
        foreach (var step in steps)
        {
            if (step.SubSteps is not { Count: > 0 })
                continue;

            var parentFp = StepFingerprint.Compute(step);
            foreach (var sub in step.SubSteps)
            {
                var subFp = StepFingerprint.Compute(sub);
                graph.RecordTransition(parentFp, subFp, EdgeKind.SubStep, workflowName, TimeSpan.Zero);
            }

            // Recurse for nested children
            RecordSubStepEdges(graph, step.SubSteps, workflowName);
        }
    }
}
