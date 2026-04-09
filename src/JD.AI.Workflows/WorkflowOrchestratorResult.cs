using JD.AI.Workflows.History;

namespace JD.AI.Workflows;

/// <summary>
/// Outcome of the orchestrator pipeline: pass-through, planning needed,
/// executed successfully, or execution failed.
/// </summary>
public enum WorkflowOutcome
{
    /// <summary>Not a workflow — route to agent normally.</summary>
    PassThrough,

    /// <summary>Workflow detected but not in catalog — prompt user to plan it.</summary>
    PlanningNeeded,

    /// <summary>Workflow found and executed successfully.</summary>
    Executed,

    /// <summary>Workflow found but execution failed.</summary>
    ExecutionFailed,
}

/// <summary>
/// Result of the full detect-match-execute pipeline.
/// </summary>
public sealed class WorkflowOrchestratorResult
{
    public WorkflowOutcome Outcome { get; init; }
    public IntentClassification Intent { get; init; } = new(false, 0.0, []);
    public WorkflowMatchResult? Match { get; init; }
    public WorkflowBridgeResult? ExecutionResult { get; init; }
    public string? PlanningPrompt { get; init; }
    public HistoryAdvisory? Advisory { get; init; }

    /// <summary>Not a workflow — pass through to normal agent handling.</summary>
    public static WorkflowOrchestratorResult PassThrough(IntentClassification intent) =>
        new()
        {
            Outcome = WorkflowOutcome.PassThrough,
            Intent = intent,
        };

    /// <summary>Workflow detected but no catalog match — needs planning.</summary>
    public static WorkflowOrchestratorResult PlanningNeeded(
        IntentClassification intent,
        string prompt,
        HistoryAdvisory? advisory = null) =>
        new()
        {
            Outcome = WorkflowOutcome.PlanningNeeded,
            Intent = intent,
            PlanningPrompt = prompt,
            Advisory = advisory,
        };

    /// <summary>Workflow matched and executed successfully.</summary>
    public static WorkflowOrchestratorResult Executed(
        IntentClassification intent,
        WorkflowMatchResult match,
        WorkflowBridgeResult result,
        HistoryAdvisory? advisory = null) =>
        new()
        {
            Outcome = WorkflowOutcome.Executed,
            Intent = intent,
            Match = match,
            ExecutionResult = result,
            Advisory = advisory,
        };

    /// <summary>Workflow matched but execution failed.</summary>
    public static WorkflowOrchestratorResult Failed(
        IntentClassification intent,
        WorkflowMatchResult match,
        WorkflowBridgeResult result,
        HistoryAdvisory? advisory = null) =>
        new()
        {
            Outcome = WorkflowOutcome.ExecutionFailed,
            Intent = intent,
            Match = match,
            ExecutionResult = result,
            Advisory = advisory,
        };
}
