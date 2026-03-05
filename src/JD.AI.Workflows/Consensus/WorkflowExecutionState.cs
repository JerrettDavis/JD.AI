using System.Collections.Concurrent;
using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows.Consensus;

/// <summary>
/// Tracks workflow execution state with checkpoint support for resume-from-failure.
/// Each execution run stores per-step completion status and intermediate results,
/// enabling workflows to resume from the last successful checkpoint.
/// </summary>
public sealed class WorkflowExecutionState
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    private readonly string _stateDirectory;
    private readonly ConcurrentDictionary<string, ExecutionRun> _runs = new(StringComparer.Ordinal);

    public WorkflowExecutionState(string stateDirectory)
    {
        _stateDirectory = stateDirectory;
        Directory.CreateDirectory(stateDirectory);
    }

    /// <summary>Starts a new execution run for a workflow.</summary>
    public ExecutionRun StartRun(string workflowName, string version, string? initiator = null)
    {
        var run = new ExecutionRun
        {
            RunId = Guid.NewGuid().ToString("N")[..16],
            WorkflowName = workflowName,
            WorkflowVersion = version,
            Initiator = initiator ?? Environment.UserName,
            StartedAt = DateTimeOffset.UtcNow,
            Status = ExecutionStatus.Running,
        };

        _runs[run.RunId] = run;
        return run;
    }

    /// <summary>Records a checkpoint for a completed step within a run.</summary>
    public void Checkpoint(string runId, string stepCorrelationId, string? result = null)
    {
        if (!_runs.TryGetValue(runId, out var run))
            throw new KeyNotFoundException($"Run '{runId}' not found");

        run.Checkpoints.Add(new StepCheckpoint
        {
            StepCorrelationId = stepCorrelationId,
            CompletedAt = DateTimeOffset.UtcNow,
            Result = result,
        });

        run.LastCheckpointAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks a run as completed successfully.</summary>
    public void Complete(string runId)
    {
        if (!_runs.TryGetValue(runId, out var run))
            throw new KeyNotFoundException($"Run '{runId}' not found");

        run.Status = ExecutionStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Marks a run as failed with an error message.</summary>
    public void Fail(string runId, string error)
    {
        if (!_runs.TryGetValue(runId, out var run))
            throw new KeyNotFoundException($"Run '{runId}' not found");

        run.Status = ExecutionStatus.Failed;
        run.Error = error;
        run.CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the current state of a run.</summary>
    public ExecutionRun? GetRun(string runId) =>
        _runs.TryGetValue(runId, out var run) ? run : null;

    /// <summary>
    /// Finds the last incomplete step in a failed run, enabling resume-from-checkpoint.
    /// Returns the correlation ID of the step to resume from, or null if all steps completed.
    /// </summary>
    public string? GetResumePoint(string runId, IList<AgentStepDefinition> steps)
    {
        if (!_runs.TryGetValue(runId, out var run))
            return null;

        var completedIds = new HashSet<string>(
            run.Checkpoints.Select(c => c.StepCorrelationId),
            StringComparer.Ordinal);

        return steps
            .FirstOrDefault(s => !completedIds.Contains(s.CorrelationId))
            ?.CorrelationId;
    }

    /// <summary>Lists all runs for a given workflow.</summary>
    public IReadOnlyList<ExecutionRun> ListRuns(string workflowName) =>
        _runs.Values
            .Where(r => string.Equals(r.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.StartedAt)
            .ToList();

    /// <summary>Persists the current run state to disk for crash recovery.</summary>
    public async Task PersistAsync(string runId, CancellationToken ct = default)
    {
        if (!_runs.TryGetValue(runId, out var run))
            return;

        var path = Path.Combine(_stateDirectory, $"{runId}.json");
        var json = JsonSerializer.Serialize(run, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    /// <summary>Loads a previously persisted run from disk.</summary>
    public async Task<ExecutionRun?> LoadAsync(string runId, CancellationToken ct = default)
    {
        var path = Path.Combine(_stateDirectory, $"{runId}.json");
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var run = JsonSerializer.Deserialize<ExecutionRun>(json, JsonOptions);
        if (run is not null)
            _runs[run.RunId] = run;

        return run;
    }
}

/// <summary>A single workflow execution run.</summary>
public sealed class ExecutionRun
{
    public string RunId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string WorkflowVersion { get; init; } = string.Empty;
    public string Initiator { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LastCheckpointAt { get; set; }
    public ExecutionStatus Status { get; set; }
    public string? Error { get; set; }
    public IList<StepCheckpoint> Checkpoints { get; init; } = [];
}

/// <summary>Checkpoint for a completed step within an execution run.</summary>
public sealed class StepCheckpoint
{
    public string StepCorrelationId { get; init; } = string.Empty;
    public DateTimeOffset CompletedAt { get; init; }
    public string? Result { get; init; }
}

/// <summary>Status of a workflow execution run.</summary>
public enum ExecutionStatus
{
    Running,
    Completed,
    Failed,
    Cancelled,
}
