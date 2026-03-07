namespace JD.AI.Core.Usage;

/// <summary>
/// Centralized usage metering service for tracking token consumption,
/// cost estimation, and budget enforcement across all providers.
/// </summary>
public interface IUsageMeter
{
    /// <summary>Records a single agent turn's usage.</summary>
    Task RecordTurnAsync(TurnUsageRecord record, CancellationToken ct = default);

    /// <summary>Gets aggregated usage for a session.</summary>
    Task<UsageSummary> GetSessionUsageAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Gets aggregated usage for a project path.</summary>
    Task<UsageSummary> GetProjectUsageAsync(string projectPath, CancellationToken ct = default);

    /// <summary>Gets aggregated usage for a time period.</summary>
    Task<UsageSummary> GetPeriodUsageAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken ct = default);

    /// <summary>Gets total lifetime usage.</summary>
    Task<UsageSummary> GetTotalUsageAsync(CancellationToken ct = default);

    /// <summary>Checks current budget status against configured limits.</summary>
    Task<BudgetStatus> CheckBudgetAsync(BudgetPeriod period = BudgetPeriod.Monthly, CancellationToken ct = default);

    /// <summary>Exports usage data in the specified format.</summary>
    Task<string> ExportAsync(UsageExportFormat format, DateTimeOffset? from = null, DateTimeOffset? until = null, CancellationToken ct = default);
}
