namespace JD.AI.Workflows.History;

/// <summary>
/// Observes workflow executions and ingests them into the workflow history graph.
/// </summary>
public interface IWorkflowHistoryObserver
{
    /// <summary>
    /// Ingest a completed workflow run into the history graph.
    /// </summary>
    /// <param name="definition">The workflow definition that was executed.</param>
    /// <param name="result">The result of the workflow execution.</param>
    /// <param name="ct">Cancellation token.</param>
    Task IngestRunAsync(
        AgentWorkflowDefinition definition,
        WorkflowBridgeResult result,
        CancellationToken ct = default);
}
