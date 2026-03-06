using FluentAssertions;
using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantQuotaModelTests
{
    // ── QuotaExceededAction enum ─────────────────────────────────────────

    [Theory]
    [InlineData(QuotaExceededAction.Reject, 0)]
    [InlineData(QuotaExceededAction.Queue, 1)]
    [InlineData(QuotaExceededAction.Degrade, 2)]
    public void QuotaExceededAction_Values(QuotaExceededAction action, int expected) =>
        ((int)action).Should().Be(expected);

    // ── TenantQuotaConfig defaults ───────────────────────────────────────

    [Fact]
    public void TenantQuotaConfig_Defaults()
    {
        var config = new TenantQuotaConfig();
        config.MaxRequestsPerWindow.Should().Be(1000);
        config.WindowDuration.Should().Be(TimeSpan.FromHours(1));
        config.MaxTokensPerDay.Should().Be(0);
        config.MaxConcurrentSessions.Should().Be(0);
        config.OnQuotaExceeded.Should().Be(QuotaExceededAction.Reject);
    }

    [Fact]
    public void TenantQuotaConfig_CustomValues()
    {
        var config = new TenantQuotaConfig
        {
            MaxRequestsPerWindow = 500,
            WindowDuration = TimeSpan.FromMinutes(30),
            MaxTokensPerDay = 1_000_000,
            MaxConcurrentSessions = 10,
            OnQuotaExceeded = QuotaExceededAction.Degrade,
        };

        config.MaxRequestsPerWindow.Should().Be(500);
        config.WindowDuration.Should().Be(TimeSpan.FromMinutes(30));
        config.MaxTokensPerDay.Should().Be(1_000_000);
        config.MaxConcurrentSessions.Should().Be(10);
        config.OnQuotaExceeded.Should().Be(QuotaExceededAction.Degrade);
    }

    // ── TenantUsageStats record ──────────────────────────────────────────

    [Fact]
    public void TenantUsageStats_Construction()
    {
        var stats = new TenantUsageStats(
            CurrentRequests: 50,
            MaxRequests: 1000,
            WindowDuration: TimeSpan.FromHours(1),
            CurrentTokensToday: 500_000,
            MaxTokensPerDay: 1_000_000,
            CurrentSessions: 3,
            MaxConcurrentSessions: 10);

        stats.CurrentRequests.Should().Be(50);
        stats.MaxRequests.Should().Be(1000);
        stats.WindowDuration.Should().Be(TimeSpan.FromHours(1));
        stats.CurrentTokensToday.Should().Be(500_000);
        stats.MaxTokensPerDay.Should().Be(1_000_000);
        stats.CurrentSessions.Should().Be(3);
        stats.MaxConcurrentSessions.Should().Be(10);
    }

    [Fact]
    public void TenantUsageStats_RecordEquality()
    {
        var a = new TenantUsageStats(10, 100, TimeSpan.FromHours(1), 500, 1000, 2, 5);
        var b = new TenantUsageStats(10, 100, TimeSpan.FromHours(1), 500, 1000, 2, 5);
        a.Should().Be(b);
    }

    [Fact]
    public void TenantUsageStats_RecordInequality()
    {
        var a = new TenantUsageStats(10, 100, TimeSpan.FromHours(1), 500, 1000, 2, 5);
        var b = new TenantUsageStats(20, 100, TimeSpan.FromHours(1), 500, 1000, 2, 5);
        a.Should().NotBe(b);
    }

    // ── TenantQuota behavioral tests ─────────────────────────────────────

    [Fact]
    public void TenantQuota_DefaultConfig_NotOverQuota()
    {
        var quota = new TenantQuota();
        quota.IsOverRequestQuota("t1").Should().BeFalse();
        quota.IsOverTokenQuota("t1").Should().BeFalse();
        quota.IsOverSessionQuota("t1").Should().BeFalse();
    }

    [Fact]
    public void TenantQuota_GetUsage_UnknownTenant_ReturnsDefaults()
    {
        var quota = new TenantQuota(new TenantQuotaConfig
        {
            MaxRequestsPerWindow = 100,
            MaxTokensPerDay = 50_000,
            MaxConcurrentSessions = 5,
        });

        var stats = quota.GetUsage("unknown");
        stats.CurrentRequests.Should().Be(0);
        stats.MaxRequests.Should().Be(100);
        stats.CurrentTokensToday.Should().Be(0);
        stats.MaxTokensPerDay.Should().Be(50_000);
        stats.CurrentSessions.Should().Be(0);
        stats.MaxConcurrentSessions.Should().Be(5);
    }

    [Fact]
    public void TenantQuota_RecordTokens_ZeroOrNegative_Ignored()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxTokensPerDay = 100 });
        quota.RecordTokens("t1", 0);
        quota.RecordTokens("t1", -5);

        quota.IsOverTokenQuota("t1").Should().BeFalse();
    }

    [Fact]
    public void TenantQuota_SessionAcquireRelease()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxConcurrentSessions = 2 });

        quota.AcquireSession("t1");
        quota.AcquireSession("t1");
        quota.IsOverSessionQuota("t1").Should().BeTrue();

        quota.ReleaseSession("t1");
        quota.IsOverSessionQuota("t1").Should().BeFalse();
    }

    [Fact]
    public void TenantQuota_ReleaseSession_UnknownTenant_NoOp()
    {
        var quota = new TenantQuota();
        var act = () => quota.ReleaseSession("unknown");
        act.Should().NotThrow();
    }
}
