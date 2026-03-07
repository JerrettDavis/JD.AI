using FluentAssertions;
using JD.AI.Core.Usage;

namespace JD.AI.Tests.Usage;

public sealed class BudgetConfigTests
{
    [Fact]
    public void Default_AllNullLimits()
    {
        var config = new BudgetConfig();
        config.DailyLimitUsd.Should().BeNull();
        config.WeeklyLimitUsd.Should().BeNull();
        config.MonthlyLimitUsd.Should().BeNull();
        config.SessionLimitUsd.Should().BeNull();
        config.WarningThresholdPercent.Should().Be(0.80m);
    }

    [Fact]
    public void AllProperties_Roundtrip()
    {
        var config = new BudgetConfig
        {
            DailyLimitUsd = 10m,
            WeeklyLimitUsd = 50m,
            MonthlyLimitUsd = 200m,
            SessionLimitUsd = 5m,
            WarningThresholdPercent = 0.90m,
        };
        config.DailyLimitUsd.Should().Be(10m);
        config.WeeklyLimitUsd.Should().Be(50m);
        config.MonthlyLimitUsd.Should().Be(200m);
        config.SessionLimitUsd.Should().Be(5m);
        config.WarningThresholdPercent.Should().Be(0.90m);
    }
}
