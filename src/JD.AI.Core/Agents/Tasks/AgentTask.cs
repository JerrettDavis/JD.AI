namespace JD.AI.Core.Agents.Tasks;

public enum AgentTaskType
{
    LocalBash,
    LocalAgent,
    Workflow,
    Dream
}

public enum AgentTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public interface IAgentTask
{
    string Id { get; }
    AgentTaskType Type { get; }
    AgentTaskStatus Status { get; }
    string? Description { get; }
    DateTimeOffset StartTime { get; }
    CancellationToken Ct { get; }
    Task<string> ExecuteAsync(CancellationToken ct);
}

public sealed class AgentTask(
    string id,
    AgentTaskType type,
    AgentTaskStatus status,
    string? description,
    DateTimeOffset startTime,
    Func<CancellationToken, Task<string>> execute,
    CancellationToken ct) : IAgentTask
{
    public string Id => id;
    public AgentTaskType Type => type;
    public AgentTaskStatus Status { get; set; } = status;
    public string? Description => description;
    public DateTimeOffset StartTime => startTime;
    public CancellationToken Ct => ct;
    public Func<CancellationToken, Task<string>> Execute => execute;

    public async Task<string> ExecuteAsync(CancellationToken ct)
    {
        // Link the caller-supplied CT with the task's own CT so that cancellation from
        // either source (e.g. the task registry cancelling the task, or the caller aborting
        // the request) is respected.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, Ct);
        return await execute(linked.Token);
    }
}
