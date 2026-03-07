using System.Collections.Concurrent;

namespace JD.AI.Core.Tracing;

/// <summary>
/// Records a timeline of operations within a single turn for the <c>/trace</c> command.
/// Thread-safe for concurrent tool invocations.
/// </summary>
public sealed class ExecutionTimeline
{
    private readonly ConcurrentBag<TimelineEntry> _entries = new();

    /// <summary>All recorded entries, ordered by start time.</summary>
    public IReadOnlyList<TimelineEntry> Entries =>
        _entries.OrderBy(static e => e.StartTime).ToList();

    /// <summary>Total wall-clock duration of all entries.</summary>
    public TimeSpan TotalDuration =>
        _entries.IsEmpty
            ? TimeSpan.Zero
            : _entries.Max(static e => e.EndTime) - _entries.Min(static e => e.StartTime);

    /// <summary>Begins recording an operation and returns a handle to complete it.</summary>
    public TimelineEntry BeginOperation(
        string operation,
        string? parentSpanId = null,
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Simple attribute bag")]
        Dictionary<string, string>? attributes = null)
    {
        var entry = new TimelineEntry
        {
            SpanId = Guid.NewGuid().ToString("N")[..16],
            ParentSpanId = parentSpanId,
            Operation = operation,
            StartTime = DateTimeOffset.UtcNow,
        };
        if (attributes is not null)
        {
            foreach (var (key, value) in attributes)
                entry.Attributes[key] = value;
        }
        _entries.Add(entry);
        return entry;
    }
}

/// <summary>
/// A single operation in the execution timeline.
/// </summary>
public sealed class TimelineEntry
{
    public string SpanId { get; init; } = string.Empty;
    public string? ParentSpanId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration => EndTime > StartTime ? EndTime - StartTime : TimeSpan.Zero;
    public string Status { get; set; } = "ok";
    public string? ErrorMessage { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Attributes are a simple key-value bag")]
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);

    /// <summary>Completes this entry, recording end time and status.</summary>
    public void Complete(string status = "ok", string? error = null)
    {
        EndTime = DateTimeOffset.UtcNow;
        Status = status;
        ErrorMessage = error;
    }
}
