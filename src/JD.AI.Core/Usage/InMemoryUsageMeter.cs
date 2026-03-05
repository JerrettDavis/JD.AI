using System.Collections.Concurrent;

namespace JD.AI.Core.Usage;

/// <summary>
/// In-memory usage meter for testing and single-node deployments.
/// Thread-safe via <see cref="ConcurrentBag{T}"/>.
/// </summary>
public sealed class InMemoryUsageMeter : IUsageMeter
{
    private readonly ConcurrentBag<TurnUsageRecord> _records = [];

    /// <summary>Total number of recorded turns.</summary>
    public int RecordCount => _records.Count;

    public Task RecordTurnAsync(TurnUsageRecord record, CancellationToken ct = default)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }

    public Task<UsageSummary> GetSessionUsageAsync(string sessionId, CancellationToken ct = default)
    {
        var records = _records.Where(r =>
            string.Equals(r.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(Summarize(records));
    }

    public Task<UsageSummary> GetProjectUsageAsync(string projectPath, CancellationToken ct = default)
    {
        var records = _records.Where(r =>
            string.Equals(r.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(Summarize(records));
    }

    public Task<UsageSummary> GetPeriodUsageAsync(
        DateTimeOffset from, DateTimeOffset until, CancellationToken ct = default)
    {
        var records = _records.Where(r => r.Timestamp >= from && r.Timestamp <= until);
        return Task.FromResult(Summarize(records));
    }

    public Task<UsageSummary> GetTotalUsageAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Summarize(_records));
    }

    public Task<BudgetStatus> CheckBudgetAsync(
        BudgetPeriod period = BudgetPeriod.Monthly, CancellationToken ct = default)
    {
        return Task.FromResult(new BudgetStatus
        {
            Period = period,
            SpentUsd = 0m, // No cost estimation in in-memory meter
        });
    }

    public Task<string> ExportAsync(
        UsageExportFormat format, DateTimeOffset? from = null, DateTimeOffset? until = null,
        CancellationToken ct = default)
    {
        return Task.FromResult($"InMemoryUsageMeter: {_records.Count} records");
    }

    private static UsageSummary Summarize(IEnumerable<TurnUsageRecord> records)
    {
        var list = records.ToList();
        return new UsageSummary
        {
            TotalTurns = list.Count,
            TotalPromptTokens = list.Sum(r => r.PromptTokens),
            TotalCompletionTokens = list.Sum(r => r.CompletionTokens),
            TotalToolCalls = list.Sum(r => r.ToolCalls),
        };
    }
}
