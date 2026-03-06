using FluentAssertions;
using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantQuotaTests
{
    [Fact]
    public void IsOverRequestQuota_NoRequests_ReturnsFalse()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxRequestsPerWindow = 10 });

        quota.IsOverRequestQuota("tenant-a").Should().BeFalse();
    }

    [Fact]
    public void IsOverRequestQuota_UnderLimit_ReturnsFalse()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxRequestsPerWindow = 5 });

        for (var i = 0; i < 3; i++)
            quota.RecordRequest("tenant-a");

        quota.IsOverRequestQuota("tenant-a").Should().BeFalse();
    }

    [Fact]
    public void IsOverRequestQuota_AtLimit_ReturnsTrue()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxRequestsPerWindow = 3 });

        for (var i = 0; i < 3; i++)
            quota.RecordRequest("tenant-a");

        quota.IsOverRequestQuota("tenant-a").Should().BeTrue();
    }

    [Fact]
    public void IsOverRequestQuota_Unlimited_NeverTrue()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxRequestsPerWindow = 0 });

        for (var i = 0; i < 100; i++)
            quota.RecordRequest("tenant-a");

        quota.IsOverRequestQuota("tenant-a").Should().BeFalse();
    }

    [Fact]
    public void IsOverRequestQuota_IsolatedPerTenant()
    {
        var quota = new TenantQuota(new TenantQuotaConfig { MaxRequestsPerWindow = 2 });

        quota.RecordRequest("tenant-a");
        quota.RecordRequest("tenant-a");
        quota.RecordRequest("tenant-b");

        quota.IsOverRequestQuota("tenant-a").Should().BeTrue();
        quota.IsOverRequestQuota("tenant-b").Should().BeFalse();
    }

    [Fact]
    public void GetUsage_ReturnsCorrectStats()
    {
        var quota = new TenantQuota(new TenantQuotaConfig
        {
            MaxRequestsPerWindow = 100,
            WindowDuration = TimeSpan.FromMinutes(5),
        });

        quota.RecordRequest("tenant-a");
        quota.RecordRequest("tenant-a");

        var usage = quota.GetUsage("tenant-a");

        usage.CurrentRequests.Should().Be(2);
        usage.MaxRequests.Should().Be(100);
        usage.WindowDuration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GetUsage_UnknownTenant_ReturnsZero()
    {
        var quota = new TenantQuota();
        var usage = quota.GetUsage("unknown");

        usage.CurrentRequests.Should().Be(0);
    }
}
