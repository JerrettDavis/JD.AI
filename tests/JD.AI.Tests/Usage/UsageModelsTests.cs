using FluentAssertions;
using JD.AI.Core.Usage;

namespace JD.AI.Tests.Usage;

public sealed class UsageModelsTests
{
    // ── TurnUsageRecord ─────────────────────────────────────────────────

    [Fact]
    public void TurnUsageRecord_TotalTokens_Computed()
    {
        var record = new TurnUsageRecord
        {
            SessionId = "s1",
            ProviderId = "p1",
            ModelId = "m1",
            PromptTokens = 500,
            CompletionTokens = 200,
        };
        record.TotalTokens.Should().Be(700);
    }

    [Fact]
    public void TurnUsageRecord_Defaults()
    {
        var record = new TurnUsageRecord
        {
            SessionId = "s",
            ProviderId = "p",
            ModelId = "m",
            PromptTokens = 0,
            CompletionTokens = 0,
        };
        record.ToolCalls.Should().Be(0);
        record.DurationMs.Should().Be(0);
        record.ProjectPath.Should().BeNull();
        record.TraceId.Should().BeNull();
        record.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── ProviderUsageBreakdown ──────────────────────────────────────────

    [Fact]
    public void ProviderUsageBreakdown_TotalTokens_Computed()
    {
        var breakdown = new ProviderUsageBreakdown
        {
            ProviderId = "anthropic",
            PromptTokens = 1000,
            CompletionTokens = 500,
            Turns = 3,
            EstimatedCostUsd = 0.05m,
        };
        breakdown.TotalTokens.Should().Be(1500);
    }

    // ── UsageSummary ────────────────────────────────────────────────────

    [Fact]
    public void UsageSummary_TotalTokens_Computed()
    {
        var summary = new UsageSummary
        {
            TotalPromptTokens = 10000,
            TotalCompletionTokens = 5000,
            TotalTurns = 20,
        };
        summary.TotalTokens.Should().Be(15000);
    }

    [Fact]
    public void UsageSummary_DefaultByProvider_Empty()
    {
        var summary = new UsageSummary();
        summary.ByProvider.Should().BeEmpty();
    }

    // ── BudgetStatus (Usage namespace) ──────────────────────────────────

    [Fact]
    public void BudgetStatus_IsExceeded_WhenSpentExceedsLimit()
    {
        var status = new BudgetStatus { SpentUsd = 15m, LimitUsd = 10m };
        status.IsExceeded.Should().BeTrue();
    }

    [Fact]
    public void BudgetStatus_IsExceeded_WhenSpentEqualsLimit()
    {
        var status = new BudgetStatus { SpentUsd = 10m, LimitUsd = 10m };
        status.IsExceeded.Should().BeTrue();
    }

    [Fact]
    public void BudgetStatus_IsNotExceeded_WhenBelowLimit()
    {
        var status = new BudgetStatus { SpentUsd = 5m, LimitUsd = 10m };
        status.IsExceeded.Should().BeFalse();
    }

    [Fact]
    public void BudgetStatus_IsNotExceeded_WhenNoLimit()
    {
        var status = new BudgetStatus { SpentUsd = 999m, LimitUsd = null };
        status.IsExceeded.Should().BeFalse();
    }

    [Fact]
    public void BudgetStatus_IsWarning_WhenAboveThreshold()
    {
        var status = new BudgetStatus { SpentUsd = 8m, WarningThresholdUsd = 7m };
        status.IsWarning.Should().BeTrue();
    }

    [Fact]
    public void BudgetStatus_IsNotWarning_WhenNoThreshold()
    {
        var status = new BudgetStatus { SpentUsd = 100m, WarningThresholdUsd = null };
        status.IsWarning.Should().BeFalse();
    }

    [Fact]
    public void BudgetStatus_RemainingUsd_Calculated()
    {
        var status = new BudgetStatus { SpentUsd = 3m, LimitUsd = 10m };
        status.RemainingUsd.Should().Be(7m);
    }

    [Fact]
    public void BudgetStatus_RemainingUsd_NeverNegative()
    {
        var status = new BudgetStatus { SpentUsd = 15m, LimitUsd = 10m };
        status.RemainingUsd.Should().Be(0m);
    }

    [Fact]
    public void BudgetStatus_RemainingUsd_NoLimit_ReturnsMax()
    {
        var status = new BudgetStatus { SpentUsd = 5m, LimitUsd = null };
        status.RemainingUsd.Should().Be(decimal.MaxValue);
    }

    [Fact]
    public void BudgetStatus_DefaultPeriod_Monthly()
    {
        var status = new BudgetStatus();
        status.Period.Should().Be(BudgetPeriod.Monthly);
    }

    // ── BudgetPeriod enum ───────────────────────────────────────────────

    [Theory]
    [InlineData(BudgetPeriod.Daily, 0)]
    [InlineData(BudgetPeriod.Weekly, 1)]
    [InlineData(BudgetPeriod.Monthly, 2)]
    [InlineData(BudgetPeriod.Session, 3)]
    public void BudgetPeriod_Values(BudgetPeriod period, int expected) =>
        ((int)period).Should().Be(expected);

    // ── UsageExportFormat enum ──────────────────────────────────────────

    [Theory]
    [InlineData(UsageExportFormat.Csv, 0)]
    [InlineData(UsageExportFormat.Json, 1)]
    public void UsageExportFormat_Values(UsageExportFormat format, int expected) =>
        ((int)format).Should().Be(expected);
}
