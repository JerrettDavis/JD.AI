using System.Text;
using JD.AI.Core.Tracing;

namespace JD.AI.Core.Memory;

/// <summary>
/// Background consolidation service that periodically reads recent daily logs
/// and updates the long-term <c>MEMORY.md</c> file for each project.
/// </summary>
public sealed class MemoryConsolidator : IDisposable
{
    private readonly IMemoryService _memory;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts;
    private readonly Task _runTask;
    private bool _disposed;

    /// <summary>
    /// Creates a consolidator that runs in the background, updating MEMORY.md
    /// files every <paramref name="checkInterval"/>.
    /// </summary>
    /// <param name="memory">The memory service to read/write.</param>
    /// <param name="checkInterval">How often to check for consolidation (default: 1 hour).</param>
    public MemoryConsolidator(IMemoryService memory, TimeSpan? checkInterval = null)
    {
        _memory = memory;
        _interval = checkInterval ?? TimeSpan.FromHours(1);
        _cts = new CancellationTokenSource();
        _runTask = Task.Run(RunAsync, CancellationToken.None);
    }

    /// <summary>
    /// Triggers an immediate consolidation for all known projects.
    /// Called internally on the background schedule; safe to invoke manually.
    /// </summary>
    public async Task ConsolidateAsync(CancellationToken ct = default)
    {
        try
        {
            var root = _memory.MemoryRoot;
            if (!Directory.Exists(root))
                return;

            foreach (var projectDir in Directory.EnumerateDirectories(root))
            {
                var projectId = Path.GetFileName(projectDir);
                await ConsolidateProjectAsync(projectId, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Memory,
                "Consolidation sweep failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Consolidates a single project's daily logs into its MEMORY.md file.
    /// Reads logs from the last 7 days and appends a summary section.
    /// </summary>
    public async Task ConsolidateProjectAsync(string projectId, CancellationToken ct = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var sb = new StringBuilder();

            // Read the existing MEMORY.md content (if any)
            var existing = await _memory.GetMemoryContentAsync(projectId, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                sb.AppendLine(existing.TrimEnd());
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine($"# Memory Consolidation — {now:yyyy-MM-dd}");
            sb.AppendLine();

            // Collect last 7 days of logs
            var logEntries = new List<string>();
            for (var i = 0; i < 7; i++)
            {
                var date = now.AddDays(-i);
                var log = await _memory.GetDailyLogAsync(projectId, date, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(log))
                {
                    foreach (var line in log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            logEntries.Add(line.Trim());
                    }
                }
            }

            if (logEntries.Count > 0)
            {
                sb.AppendLine("## Recent Turns");
                sb.AppendLine();
                foreach (var entry in logEntries.TakeLast(50))
                {
                    sb.AppendLine($"- {entry}");
                }
            }
            else
            {
                sb.AppendLine("_No turns recorded in the last 7 days._");
            }

            await _memory.WriteMemoryContentAsync(projectId, sb.ToString(), ct).ConfigureAwait(false);

            DebugLogger.Log(DebugCategory.Memory,
                "Consolidated {0} log entries for project {1}", logEntries.Count, projectId);
        }
        catch (Exception ex)
        {
            DebugLogger.Log(DebugCategory.Memory,
                "Failed to consolidate project {0}: {1}", projectId, ex.Message);
        }
    }

    private async Task RunAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, _cts.Token).ConfigureAwait(false);
                await ConsolidateAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                DebugLogger.Log(DebugCategory.Memory,
                    "Consolidation background task error: {0}", ex.Message);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
