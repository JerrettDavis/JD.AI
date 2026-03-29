namespace JD.AI.Workflows;

/// <summary>
/// Result of executing an agent workflow through the WorkflowBridge.
/// </summary>
public sealed class WorkflowBridgeResult
{
    /// <summary>Whether the workflow completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>The final output produced by the last step.</summary>
    public string? FinalOutput { get; init; }

    /// <summary>Outputs captured from each individual step, keyed by step name.</summary>
    public IReadOnlyDictionary<string, string> StepOutputs { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Errors collected during workflow execution.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Total wall-clock duration of the workflow execution.</summary>
    public TimeSpan Duration { get; init; }
}
