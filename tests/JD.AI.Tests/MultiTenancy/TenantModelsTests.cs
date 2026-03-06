using FluentAssertions;
using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantModelsTests
{
    // ── TenantInfo ──────────────────────────────────────────────────────

    [Fact]
    public void TenantInfo_RequiredTenantId_Set()
    {
        var info = new TenantInfo { TenantId = "t-1" };
        info.TenantId.Should().Be("t-1");
        info.TenantName.Should().BeNull();
    }

    [Fact]
    public void TenantInfo_OptionalTenantName_Set()
    {
        var info = new TenantInfo { TenantId = "t-2", TenantName = "Org Two" };
        info.TenantId.Should().Be("t-2");
        info.TenantName.Should().Be("Org Two");
    }

    // ── TenantResolutionContext ─────────────────────────────────────────

    [Fact]
    public void TenantResolutionContext_Default_HasEmptyHeaders()
    {
        var ctx = new TenantResolutionContext();
        ctx.Headers.Should().NotBeNull().And.BeEmpty();
        ctx.UserId.Should().BeNull();
        ctx.ApiKey.Should().BeNull();
    }

    [Fact]
    public void TenantResolutionContext_Headers_CaseInsensitive()
    {
        var ctx = new TenantResolutionContext
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Tenant-Id"] = "abc",
            },
        };
        ctx.Headers["x-tenant-id"].Should().Be("abc");
        ctx.Headers["X-TENANT-ID"].Should().Be("abc");
    }

    [Fact]
    public void TenantResolutionContext_AllProperties_Roundtrip()
    {
        var ctx = new TenantResolutionContext
        {
            UserId = "user-42",
            ApiKey = "sk-test-key",
        };
        ctx.UserId.Should().Be("user-42");
        ctx.ApiKey.Should().Be("sk-test-key");
    }
}
