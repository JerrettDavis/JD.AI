namespace JD.AI.Core.Agents.Tasks;

public interface IAgentTaskRegistry
{
    IReadOnlyList<IAgentTask> ActiveTasks { get; }
    ValueTask<string> RegisterAsync(IAgentTask task, CancellationToken ct = default);
    ValueTask CancelAsync(string taskId, CancellationToken ct = default);
}

public sealed class AgentTaskRegistry : IAgentTaskRegistry
{
    private readonly Dictionary<string, IAgentTask> _tasks = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<IAgentTask> ActiveTasks
    {
        get { lock (_lock) return _tasks.Values.ToList(); }
    }

    public ValueTask<string> RegisterAsync(IAgentTask task, CancellationToken ct = default)
    {
        lock (_lock) _tasks[task.Id] = task;
        return ValueTask.FromResult(task.Id);
    }

    public ValueTask CancelAsync(string taskId, CancellationToken ct = default)
    {
        lock (_lock)
            if (_tasks.TryGetValue(taskId, out var task) && task is AgentTask at)
            {
                at.Status = AgentTaskStatus.Cancelled;
            }

        return ValueTask.CompletedTask;
    }
}
