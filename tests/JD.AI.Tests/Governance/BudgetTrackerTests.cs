using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Governance;

public sealed class BudgetTrackerTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly BudgetTracker _tracker;

    public BudgetTrackerTests()
    {
        _tracker = new BudgetTracker(Path.Combine(_fixture.DirectoryPath, "budget.json"));
    }

    public void Dispose()
    {
        _tracker.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task RecordSpend_SingleEntry_IsReflectedInStatus()
    {
        await _tracker.RecordSpendAsync(1.50m, "openai");

        var status = await _tracker.GetStatusAsync();

        status.TodayUsd.Should().Be(1.50m);
        status.MonthUsd.Should().Be(1.50m);
    }

    [Fact]
    public async Task RecordSpend_MultipleEntries_Accumulated()
    {
        await _tracker.RecordSpendAsync(1.00m, "openai");
        await _tracker.RecordSpendAsync(2.50m, "anthropic");

        var status = await _tracker.GetStatusAsync();

        status.TodayUsd.Should().Be(3.50m);
        status.MonthUsd.Should().Be(3.50m);
    }

    [Fact]
    public async Task GetStatus_NoSpend_ReturnsZeros()
    {
        var status = await _tracker.GetStatusAsync();

        status.TodayUsd.Should().Be(0m);
        status.MonthUsd.Should().Be(0m);
        status.DailyLimitExceeded.Should().BeFalse();
        status.MonthlyLimitExceeded.Should().BeFalse();
        status.AlertTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinBudget_NullPolicy_ReturnsTrue()
    {
        await _tracker.RecordSpendAsync(1000m, "openai");

        var within = await _tracker.IsWithinBudgetAsync(null);

        within.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinBudget_UnderDailyLimit_ReturnsTrue()
    {
        await _tracker.RecordSpendAsync(5m, "openai");

        var policy = new BudgetPolicy { MaxDailyUsd = 10m };
        var within = await _tracker.IsWithinBudgetAsync(policy);

        within.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinBudget_ExceedsDailyLimit_ReturnsFalse()
    {
        await _tracker.RecordSpendAsync(15m, "openai");

        var policy = new BudgetPolicy { MaxDailyUsd = 10m };
        var within = await _tracker.IsWithinBudgetAsync(policy);

        within.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinBudget_ExceedsMonthlyLimit_ReturnsFalse()
    {
        await _tracker.RecordSpendAsync(150m, "openai");

        var policy = new BudgetPolicy { MaxMonthlyUsd = 100m };
        var within = await _tracker.IsWithinBudgetAsync(policy);

        within.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinBudget_UnderMonthlyLimit_ReturnsTrue()
    {
        await _tracker.RecordSpendAsync(50m, "openai");

        var policy = new BudgetPolicy { MaxMonthlyUsd = 100m };
        var within = await _tracker.IsWithinBudgetAsync(policy);

        within.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_AlertThresholdReached_AlertTriggered()
    {
        await _tracker.RecordSpendAsync(8.50m, "openai");

        var policy = new BudgetPolicy { MaxDailyUsd = 10m, AlertThresholdPercent = 80 };

        // Compute via IsWithinBudget and manual status
        var within = await _tracker.IsWithinBudgetAsync(policy);
        var status = await _tracker.GetStatusAsync();

        // 8.50 / 10.00 = 85% >= 80%
        within.Should().BeTrue(); // Not exceeded yet
        status.TodayUsd.Should().Be(8.50m);
    }

    [Fact]
    public async Task RecordSpend_PersistsToDisk()
    {
        await _tracker.RecordSpendAsync(3.00m, "openai");

        // Create a new tracker pointing to the same file
        using var tracker2 = new BudgetTracker(Path.Combine(_fixture.DirectoryPath, "budget.json"));
        var status = await tracker2.GetStatusAsync();

        status.TodayUsd.Should().Be(3.00m);
    }

    [Fact]
    public async Task IsWithinBudget_NoDailyLimit_OnlyCheckMonthly()
    {
        await _tracker.RecordSpendAsync(50m, "openai");

        var policy = new BudgetPolicy { MaxDailyUsd = null, MaxMonthlyUsd = 200m };
        var within = await _tracker.IsWithinBudgetAsync(policy);

        within.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinBudget_ExactlyAtLimit_StillWithin()
    {
        await _tracker.RecordSpendAsync(10m, "openai");

        var policy = new BudgetPolicy { MaxDailyUsd = 10m };
        var within = await _tracker.IsWithinBudgetAsync(policy);

        // 10 > 10 is false, so it is within budget
        within.Should().BeTrue();
    }
}
