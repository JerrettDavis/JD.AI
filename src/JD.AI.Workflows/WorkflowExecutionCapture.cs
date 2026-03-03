using System.Diagnostics;
using WorkflowFramework;

namespace JD.AI.Workflows;

/// <summary>
/// Captures step-level execution events during a workflow run.
/// Implements <see cref="IWorkflowEvents"/> for direct integration with
/// <see cref="IWorkflowBuilder{T}.WithEvents"/>.
/// </summary>
public sealed class WorkflowExecutionCapture : WorkflowEventsBase
{
    private readonly List<StepEvent> _events = [];
    private readonly Dictionary<string, Stopwatch> _timers = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<StepEvent> Events
    {
        get { lock (_lock) return [.. _events]; }
    }

    public int CompletedCount
    {
        get { lock (_lock) return _events.Count(e => e.Kind == StepEventKind.Completed); }
    }

    public int FailedCount
    {
        get { lock (_lock) return _events.Count(e => e.Kind == StepEventKind.Failed); }
    }

    public override Task OnStepStartedAsync(IWorkflowContext context, IStep step)
    {
        lock (_lock)
        {
            _events.Add(new StepEvent(step.Name, StepEventKind.Started, DateTimeOffset.UtcNow));
            _timers[step.Name] = Stopwatch.StartNew();
        }

        return Task.CompletedTask;
    }

    public override Task OnStepCompletedAsync(IWorkflowContext context, IStep step)
    {
        lock (_lock)
        {
            var duration = _timers.TryGetValue(step.Name, out var sw)
                ? sw.Elapsed
                : TimeSpan.Zero;
            _events.Add(new StepEvent(step.Name, StepEventKind.Completed, DateTimeOffset.UtcNow, duration));
        }

        return Task.CompletedTask;
    }

    public override Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception)
    {
        lock (_lock)
        {
            var duration = _timers.TryGetValue(step.Name, out var sw)
                ? sw.Elapsed
                : TimeSpan.Zero;
            _events.Add(new StepEvent(step.Name, StepEventKind.Failed, DateTimeOffset.UtcNow, duration, exception.Message));
        }

        return Task.CompletedTask;
    }
}

public enum StepEventKind { Started, Completed, Failed }

public sealed record StepEvent(
    string StepName,
    StepEventKind Kind,
    DateTimeOffset Timestamp,
    TimeSpan? Duration = null,
    string? Error = null);
