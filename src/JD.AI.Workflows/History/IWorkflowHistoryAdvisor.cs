namespace JD.AI.Workflows.History;

public interface IWorkflowHistoryAdvisor
{
    Task<HistoryAdvisory> AdviseAsync(AgentWorkflowDefinition definition, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowPathSummary>> FindWorkflowsThroughStepAsync(string stepNameOrFingerprint, int limit = 20, CancellationToken ct = default);
    Task<IReadOnlyList<WeightedPath>> GetCommonPathsFromAsync(string stepNameOrFingerprint, int topN = 5, int maxDepth = 10, CancellationToken ct = default);
    Task<IReadOnlyList<SimilarPathResult>> FindSimilarPathsAsync(AgentWorkflowDefinition definition, int topN = 5, CancellationToken ct = default);
}

public sealed class HistoryAdvisory
{
    public bool HasHistory { get; init; }
    public IReadOnlyList<StepAdvisory> StepAdvisories { get; init; } = [];
    public IReadOnlyList<SimilarPathResult> SimilarPaths { get; init; } = [];
    public double FamiliarityScore { get; init; }
}

public sealed class StepAdvisory
{
    public string StepName { get; init; } = string.Empty;
    public string Fingerprint { get; init; } = string.Empty;
    public bool PreviouslySeen { get; init; }
    public double HistoricalSuccessRate { get; init; }
    public TimeSpan HistoricalAverageDuration { get; init; }
    public string? MostCommonNextStep { get; init; }
}

public sealed class WeightedPath
{
    public IReadOnlyList<WorkflowHistoryNode> Steps { get; init; } = [];
    public long TotalWeight { get; init; }
    public double AverageSuccessRate { get; init; }
}

public sealed class WorkflowPathSummary
{
    public string WorkflowName { get; init; } = string.Empty;
    public int StepCount { get; init; }
    public long ExecutionCount { get; init; }
    public double SuccessRate { get; init; }
}

public sealed class SimilarPathResult
{
    public double Similarity { get; init; }
    public string WorkflowName { get; init; } = string.Empty;
    public IReadOnlyList<string> SharedStepNames { get; init; } = [];
    public IReadOnlyList<string> UniqueToCandidate { get; init; } = [];
    public long TotalExecutions { get; init; }
    public double SuccessRate { get; init; }
}
