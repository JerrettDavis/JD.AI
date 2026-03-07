using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class BudgetStatusTests
{
    [Fact]
    public void Default_AllZeroAndFalse()
    {
        var status = new BudgetStatus();
        status.TodayUsd.Should().Be(0m);
        status.MonthUsd.Should().Be(0m);
        status.DailyLimitExceeded.Should().BeFalse();
        status.MonthlyLimitExceeded.Should().BeFalse();
        status.AlertTriggered.Should().BeFalse();
    }

    [Fact]
    public void InitProperties_Roundtrip()
    {
        var status = new BudgetStatus
        {
            TodayUsd = 12.50m,
            MonthUsd = 350.75m,
            DailyLimitExceeded = true,
            MonthlyLimitExceeded = false,
            AlertTriggered = true,
        };
        status.TodayUsd.Should().Be(12.50m);
        status.MonthUsd.Should().Be(350.75m);
        status.DailyLimitExceeded.Should().BeTrue();
        status.MonthlyLimitExceeded.Should().BeFalse();
        status.AlertTriggered.Should().BeTrue();
    }

    [Fact]
    public void AllLimitsExceeded()
    {
        var status = new BudgetStatus
        {
            TodayUsd = 100m,
            MonthUsd = 3000m,
            DailyLimitExceeded = true,
            MonthlyLimitExceeded = true,
            AlertTriggered = true,
        };
        status.DailyLimitExceeded.Should().BeTrue();
        status.MonthlyLimitExceeded.Should().BeTrue();
        status.AlertTriggered.Should().BeTrue();
    }
}
