using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantQuotaExtendedTests
{
    [Fact]
    public void IsOverTokenQuota_ReturnsFalse_WhenUnlimited()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxTokensPerDay = 0 });
        quota.RecordTokens("t1", 999_999);
        Assert.False(quota.IsOverTokenQuota("t1"));
    }

    [Fact]
    public void IsOverTokenQuota_ReturnsTrue_WhenExceeded()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxTokensPerDay = 1000 });
        quota.RecordTokens("t1", 1001);
        Assert.True(quota.IsOverTokenQuota("t1"));
    }

    [Fact]
    public void IsOverSessionQuota_ReturnsFalse_WhenUnderLimit()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxConcurrentSessions = 5 });
        quota.AcquireSession("t1");
        quota.AcquireSession("t1");
        Assert.False(quota.IsOverSessionQuota("t1"));
    }

    [Fact]
    public void IsOverSessionQuota_ReturnsTrue_WhenAtLimit()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxConcurrentSessions = 2 });
        quota.AcquireSession("t1");
        quota.AcquireSession("t1");
        Assert.True(quota.IsOverSessionQuota("t1"));
    }

    [Fact]
    public void ReleaseSession_DecrementsCount()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxConcurrentSessions = 2 });
        quota.AcquireSession("t1");
        quota.AcquireSession("t1");
        quota.ReleaseSession("t1");

        // Now at 1 of 2 — should not be over quota
        Assert.False(quota.IsOverSessionQuota("t1"));
    }

    [Fact]
    public void GetUsage_ReturnsAllStats()
    {
        var quota = new TenantQuota(new TenantQuotaConfig
        {
            MaxTokensPerDay = 10_000,
            MaxConcurrentSessions = 5,
        });
        quota.RecordTokens("t1", 3_000);
        quota.AcquireSession("t1");

        var stats = quota.GetUsage("t1");

        Assert.Equal(3_000, stats.CurrentTokensToday);
        Assert.Equal(10_000, stats.MaxTokensPerDay);
        Assert.Equal(1, stats.CurrentSessions);
        Assert.Equal(5, stats.MaxConcurrentSessions);
    }

    [Fact]
    public void QuotaExceededAction_DefaultIsReject()
    {
        var config = new TenantQuotaConfig();
        Assert.Equal(QuotaExceededAction.Reject, config.OnQuotaExceeded);
    }
}
