using System.Text.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Config;

namespace JD.AI.Core.Governance;

/// <summary>
/// Persists daily and monthly spend data as JSON to <c>~/.jdai/budget.json</c>
/// and evaluates it against configured <see cref="BudgetPolicy"/> limits.
/// </summary>
public sealed class BudgetTracker : IBudgetTracker, IDisposable
{
    private readonly string _budgetFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public BudgetTracker() : this(Path.Combine(DataDirectories.Root, "budget.json"))
    {
    }

    internal BudgetTracker(string budgetFilePath)
    {
        _budgetFilePath = budgetFilePath;
    }

    /// <inheritdoc/>
    public async Task RecordSpendAsync(decimal amountUsd, string providerName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerName);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(ct).ConfigureAwait(false);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var key = today.ToString("yyyy-MM-dd");

            if (!data.DailyEntries.TryGetValue(key, out var entry))
            {
                entry = new BudgetEntry { Date = key };
                data.DailyEntries[key] = entry;
            }

            entry.TotalUsd += amountUsd;
            if (!entry.ByProvider.TryGetValue(providerName, out var providerSpend))
                providerSpend = 0m;

            entry.ByProvider[providerName] = providerSpend + amountUsd;

            await SaveAsync(data, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<BudgetStatus> GetStatusAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(ct).ConfigureAwait(false);
            return ComputeStatus(data, policy: null);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsWithinBudgetAsync(BudgetPolicy? policy, CancellationToken ct = default)
    {
        if (policy is null)
            return true;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(ct).ConfigureAwait(false);
            var status = ComputeStatus(data, policy);
            return !status.DailyLimitExceeded && !status.MonthlyLimitExceeded;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static BudgetStatus ComputeStatus(BudgetData data, BudgetPolicy? policy)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayKey = today.ToString("yyyy-MM-dd");
        var monthPrefix = today.ToString("yyyy-MM");

        var todayUsd = data.DailyEntries.TryGetValue(todayKey, out var todayEntry)
            ? todayEntry.TotalUsd
            : 0m;

        var monthUsd = data.DailyEntries
            .Where(kvp => kvp.Key.StartsWith(monthPrefix, StringComparison.Ordinal))
            .Sum(kvp => kvp.Value.TotalUsd);

        var dailyLimitExceeded = policy?.MaxDailyUsd.HasValue == true && todayUsd > policy.MaxDailyUsd.Value;
        var monthlyLimitExceeded = policy?.MaxMonthlyUsd.HasValue == true && monthUsd > policy.MaxMonthlyUsd.Value;

        var alertThreshold = policy?.AlertThresholdPercent ?? 80;
        var alertTriggered = false;

        if (policy?.MaxDailyUsd.HasValue == true)
        {
            var dailyPct = policy.MaxDailyUsd.Value > 0
                ? (double)(todayUsd / policy.MaxDailyUsd.Value) * 100.0
                : 0.0;
            if (dailyPct >= alertThreshold)
                alertTriggered = true;
        }

        if (policy?.MaxMonthlyUsd.HasValue == true)
        {
            var monthlyPct = policy.MaxMonthlyUsd.Value > 0
                ? (double)(monthUsd / policy.MaxMonthlyUsd.Value) * 100.0
                : 0.0;
            if (monthlyPct >= alertThreshold)
                alertTriggered = true;
        }

        return new BudgetStatus
        {
            TodayUsd = todayUsd,
            MonthUsd = monthUsd,
            DailyLimitExceeded = dailyLimitExceeded,
            MonthlyLimitExceeded = monthlyLimitExceeded,
            AlertTriggered = alertTriggered,
        };
    }

    private async Task<BudgetData> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_budgetFilePath))
            return new BudgetData();

        try
        {
            await using var stream = new FileStream(_budgetFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var data = await JsonSerializer.DeserializeAsync<BudgetData>(stream, JsonOptions, ct).ConfigureAwait(false);
            return data ?? new BudgetData();
        }
        catch (JsonException)
        {
            return new BudgetData();
        }
    }

    private async Task SaveAsync(BudgetData data, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_budgetFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = new FileStream(_budgetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();

    private sealed class BudgetData
    {
        [JsonPropertyName("dailyEntries")]
        public Dictionary<string, BudgetEntry> DailyEntries { get; set; } = [];
    }

    private sealed class BudgetEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("totalUsd")]
        public decimal TotalUsd { get; set; }

        [JsonPropertyName("byProvider")]
        public Dictionary<string, decimal> ByProvider { get; set; } = [];
    }
}
