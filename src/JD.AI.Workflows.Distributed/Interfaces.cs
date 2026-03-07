namespace JD.AI.Workflows.Distributed;

/// <summary>
/// Enqueues <see cref="WorkflowWorkItem"/> instances for distributed execution.
/// Implementations back this with Redis Streams, Azure Service Bus, or an in-process channel.
/// </summary>
public interface IWorkflowDispatcher
{
    /// <summary>
    /// Dispatches a workflow work item for execution.
    /// </summary>
    /// <param name="item">The work item to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchAsync(WorkflowWorkItem item, CancellationToken ct = default);
}

/// <summary>
/// Pulls and executes <see cref="WorkflowWorkItem"/> instances from a queue.
/// Implementations should be registered as <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
/// </summary>
public interface IWorkflowWorker
{
    /// <summary>
    /// Processes a single work item. Callers control acknowledgment or dead-lettering
    /// based on the returned <see cref="WorkItemResult"/>.
    /// </summary>
    Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default);
}

/// <summary>
/// Routes dead-lettered <see cref="WorkflowWorkItem"/> instances to a durable store.
/// </summary>
public interface IDeadLetterSink
{
    /// <summary>Records a dead-lettered item with its reason.</summary>
    Task DeadLetterAsync(WorkflowWorkItem item, string reason, Exception? exception = null, CancellationToken ct = default);
}
