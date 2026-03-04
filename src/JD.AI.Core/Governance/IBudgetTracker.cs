namespace JD.AI.Core.Governance;

/// <summary>Snapshot of current spending versus configured limits.</summary>
public sealed class BudgetStatus
{
    /// <summary>Total spend today (UTC date) in USD.</summary>
    public decimal TodayUsd { get; init; }

    /// <summary>Total spend this calendar month (UTC) in USD.</summary>
    public decimal MonthUsd { get; init; }

    /// <summary>Whether the daily limit has been exceeded.</summary>
    public bool DailyLimitExceeded { get; init; }

    /// <summary>Whether the monthly limit has been exceeded.</summary>
    public bool MonthlyLimitExceeded { get; init; }

    /// <summary>Whether the alert threshold has been reached for daily or monthly spend.</summary>
    public bool AlertTriggered { get; init; }
}

/// <summary>
/// Tracks AI provider spending against daily and monthly budget limits.
/// </summary>
public interface IBudgetTracker
{
    /// <summary>Records spend for a provider interaction.</summary>
    Task RecordSpendAsync(decimal amountUsd, string providerName, CancellationToken ct = default);

    /// <summary>Returns the current spending status.</summary>
    Task<BudgetStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> when the current spend is within the configured limits.
    /// If <paramref name="policy"/> is <see langword="null"/>, always returns <see langword="true"/>.
    /// </summary>
    Task<bool> IsWithinBudgetAsync(BudgetPolicy? policy, CancellationToken ct = default);
}
