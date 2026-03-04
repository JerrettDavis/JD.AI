namespace JD.AI.Core.Usage;

/// <summary>
/// Record of token usage for a single agent turn.
/// </summary>
public sealed record TurnUsageRecord
{
    public required string SessionId { get; init; }
    public required string ProviderId { get; init; }
    public required string ModelId { get; init; }
    public required long PromptTokens { get; init; }
    public required long CompletionTokens { get; init; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public int ToolCalls { get; init; }
    public long DurationMs { get; init; }
    public string? ProjectPath { get; init; }
    public string? TraceId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Aggregated usage summary for queries.
/// </summary>
public sealed record UsageSummary
{
    public long TotalPromptTokens { get; init; }
    public long TotalCompletionTokens { get; init; }
    public long TotalTokens => TotalPromptTokens + TotalCompletionTokens;
    public int TotalTurns { get; init; }
    public int TotalToolCalls { get; init; }
    public decimal EstimatedCostUsd { get; init; }
    public IReadOnlyDictionary<string, ProviderUsageBreakdown> ByProvider { get; init; } =
        new Dictionary<string, ProviderUsageBreakdown>(StringComparer.Ordinal);
}

/// <summary>
/// Per-provider usage breakdown.
/// </summary>
public sealed record ProviderUsageBreakdown
{
    public required string ProviderId { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public int Turns { get; init; }
    public decimal EstimatedCostUsd { get; init; }
}

/// <summary>
/// Budget status check result.
/// </summary>
public sealed record BudgetStatus
{
    public decimal SpentUsd { get; init; }
    public decimal? LimitUsd { get; init; }
    public decimal? WarningThresholdUsd { get; init; }
    public bool IsExceeded => LimitUsd.HasValue && SpentUsd >= LimitUsd.Value;
    public bool IsWarning => WarningThresholdUsd.HasValue && SpentUsd >= WarningThresholdUsd.Value;
    public decimal RemainingUsd => LimitUsd.HasValue ? Math.Max(0, LimitUsd.Value - SpentUsd) : decimal.MaxValue;
    public BudgetPeriod Period { get; init; } = BudgetPeriod.Monthly;
}

/// <summary>Budget time periods.</summary>
public enum BudgetPeriod
{
    Daily,
    Weekly,
    Monthly,
    Session,
}

/// <summary>Export formats for usage data.</summary>
public enum UsageExportFormat
{
    Csv,
    Json,
}
