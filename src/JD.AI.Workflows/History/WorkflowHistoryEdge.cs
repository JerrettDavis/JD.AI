namespace JD.AI.Workflows.History;

public enum EdgeKind
{
    Sequential,
    SubStep,
    DataFlow,
}

public sealed class WorkflowHistoryEdge
{
    public string SourceFingerprint { get; init; } = string.Empty;
    public string TargetFingerprint { get; init; } = string.Empty;
    public EdgeKind Kind { get; init; }
    public long Weight { get; set; }
    public TimeSpan AverageTransitionTime { get; set; }
    public ISet<string> WorkflowNames { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; set; }
}
