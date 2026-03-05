using System.Text.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Workflows.Consensus;

/// <summary>
/// Append-only persistent history of workflow executions.
/// Each run is logged with metadata, outcome, and step-level detail
/// for auditing and compliance requirements.
/// </summary>
public sealed class WorkflowRunHistory
{
    private static readonly JsonSerializerOptions WriteOptions = JsonDefaults.Compact;
    private static readonly JsonSerializerOptions ReadOptions = JsonDefaults.Options;

    private readonly string _historyPath;
    private readonly Lock _writeLock = new();

    public WorkflowRunHistory(string historyPath)
    {
        _historyPath = historyPath;
        var dir = Path.GetDirectoryName(historyPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>Records a completed or failed execution run to the history log.</summary>
    public async Task RecordAsync(RunHistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var line = JsonSerializer.Serialize(entry, WriteOptions);

        // Append-only: one JSON object per line (JSONL format)
        lock (_writeLock)
        {
            File.AppendAllText(_historyPath, line + Environment.NewLine);
        }

        await Task.CompletedTask;
    }

    /// <summary>Queries history entries with optional filters.</summary>
    public async Task<IReadOnlyList<RunHistoryEntry>> QueryAsync(
        string? workflowName = null,
        ExecutionStatus? status = null,
        string? initiator = null,
        DateTimeOffset? since = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        if (!File.Exists(_historyPath))
            return [];

        var lines = await File.ReadAllLinesAsync(_historyPath, ct).ConfigureAwait(false);
        var entries = new List<RunHistoryEntry>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var entry = JsonSerializer.Deserialize<RunHistoryEntry>(line, ReadOptions);
                if (entry is null) continue;

                if (workflowName is not null &&
                    !string.Equals(entry.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (status.HasValue && entry.Status != status.Value)
                    continue;

                if (initiator is not null &&
                    !string.Equals(entry.Initiator, initiator, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (since.HasValue && entry.StartedAt < since.Value)
                    continue;

                entries.Add(entry);
            }
#pragma warning disable CA1031
            catch
            {
                // Skip malformed lines
            }
#pragma warning restore CA1031
        }

        return entries
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>Gets the total number of runs recorded for a workflow.</summary>
    public async Task<int> CountAsync(string? workflowName = null, CancellationToken ct = default)
    {
        var entries = await QueryAsync(workflowName: workflowName, limit: int.MaxValue, ct: ct)
            .ConfigureAwait(false);
        return entries.Count;
    }
}

/// <summary>A single entry in the workflow run history log.</summary>
public sealed class RunHistoryEntry
{
    public string RunId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string WorkflowVersion { get; init; } = string.Empty;
    public string Initiator { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public ExecutionStatus Status { get; init; }
    public string? Error { get; init; }
    public long DurationMs { get; init; }
    public int StepsCompleted { get; init; }
    public int StepsTotal { get; init; }
}
