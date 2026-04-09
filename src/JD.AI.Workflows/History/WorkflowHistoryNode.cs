namespace JD.AI.Workflows.History;

public sealed class WorkflowHistoryNode
{
    public string Fingerprint { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AgentStepKind Kind { get; init; }
    public string? Target { get; init; }
    public long ExecutionCount { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
    public ISet<string> WorkflowNames { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public double SuccessRate => ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount : 0.0;
}
