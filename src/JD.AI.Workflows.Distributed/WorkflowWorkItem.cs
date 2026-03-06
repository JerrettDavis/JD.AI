using System.Text.Json.Serialization;

namespace JD.AI.Workflows.Distributed;

/// <summary>
/// A serializable unit of work enqueued for distributed workflow execution.
/// </summary>
public sealed record WorkflowWorkItem
{
    /// <summary>Unique identifier for this work item.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Name of the workflow to execute.</summary>
    [JsonPropertyName("workflowName")]
    public required string WorkflowName { get; init; }

    /// <summary>Requested workflow version. <c>null</c> means latest.</summary>
    [JsonPropertyName("workflowVersion")]
    public string? WorkflowVersion { get; init; }

    /// <summary>Correlation ID used for distributed tracing (OTel TraceId linking).</summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Initial context passed to the workflow.</summary>
    [JsonPropertyName("initialContext")]
    public IDictionary<string, string> InitialContext { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>When the item was enqueued.</summary>
    [JsonPropertyName("enqueuedAt")]
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Execution priority (0 = highest).</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    /// <summary>Number of delivery attempts. Incremented by the worker on each retry.</summary>
    [JsonPropertyName("deliveryCount")]
    public int DeliveryCount { get; init; }

    /// <summary>Maximum delivery attempts before dead-lettering.</summary>
    [JsonPropertyName("maxDeliveryCount")]
    public int MaxDeliveryCount { get; init; } = 5;
}

/// <summary>Outcome of processing a <see cref="WorkflowWorkItem"/>.</summary>
public enum WorkItemResult
{
    /// <summary>The work item was processed successfully and should be acknowledged.</summary>
    Success,

    /// <summary>Processing failed transiently; the item should be requeued or retried.</summary>
    Transient,

    /// <summary>Processing failed permanently; the item should be dead-lettered.</summary>
    Permanent,
}
