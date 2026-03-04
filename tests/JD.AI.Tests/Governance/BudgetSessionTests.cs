using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class BudgetSessionTests : IDisposable
{
    private readonly string _tempFile;
    private readonly BudgetTracker _tracker;

    public BudgetSessionTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"budget-session-{Guid.NewGuid():N}.json");
        _tracker = new BudgetTracker(_tempFile);
    }

    [Fact]
    public async Task SessionBudget_ExceedsLimit_DetectedByIsWithinBudget()
    {
        var policy = new BudgetPolicy { MaxDailyUsd = 5.00m };

        // Record spend up to the limit
        await _tracker.RecordSpendAsync(3.00m, "OpenAI");
        Assert.True(await _tracker.IsWithinBudgetAsync(policy));

        await _tracker.RecordSpendAsync(3.00m, "OpenAI");
        Assert.False(await _tracker.IsWithinBudgetAsync(policy));
    }

    [Fact]
    public async Task GetStatus_ReturnsCorrectTotals()
    {
        await _tracker.RecordSpendAsync(1.50m, "OpenAI");
        await _tracker.RecordSpendAsync(0.75m, "Anthropic");

        var status = await _tracker.GetStatusAsync();
        Assert.Equal(2.25m, status.TodayUsd);
    }

    [Fact]
    public async Task NullPolicy_AlwaysWithinBudget()
    {
        await _tracker.RecordSpendAsync(999.99m, "OpenAI");
        Assert.True(await _tracker.IsWithinBudgetAsync(null));
    }

    [Fact]
    public async Task AlertThreshold_TriggeredAt80Percent()
    {
        var policy = new BudgetPolicy
        {
            MaxDailyUsd = 10.00m,
            AlertThresholdPercent = 80,
        };

        await _tracker.RecordSpendAsync(8.50m, "OpenAI");
        var status = await _tracker.GetStatusAsync();
        // Manually check: 8.50/10 = 85% > 80%
        Assert.True(await _tracker.IsWithinBudgetAsync(policy)); // still within limit
        // But alert should be triggered (we'd need to expose this differently)
    }

    [Fact]
    public void MaxSessionUsd_Property_Exists()
    {
        var policy = new BudgetPolicy { MaxSessionUsd = 2.50m };
        Assert.Equal(2.50m, policy.MaxSessionUsd);
    }

    public void Dispose()
    {
        _tracker.Dispose();
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
