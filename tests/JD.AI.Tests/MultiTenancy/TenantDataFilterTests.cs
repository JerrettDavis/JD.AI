using FluentAssertions;
using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantDataFilterTests
{
    private sealed record Item(string Name, string? TenantId);

    [Fact]
    public void Filter_WithActiveTenant_ReturnsOnlyTenantItems()
    {
        var ctx = new TenantContext { TenantId = "tenant-a" };
        var filter = new TenantDataFilter(ctx);

        var items = new[]
        {
            new Item("a1", "tenant-a"),
            new Item("b1", "tenant-b"),
            new Item("shared", null),
        };

        var result = filter.Filter(items, i => i.TenantId).ToList();

        result.Should().HaveCount(2);
        result.Select(i => i.Name).Should().Contain("a1").And.Contain("shared");
    }

    [Fact]
    public void Filter_NoTenantContext_ReturnsAll()
    {
        var ctx = new TenantContext();
        var filter = new TenantDataFilter(ctx);

        var items = new[]
        {
            new Item("a1", "tenant-a"),
            new Item("b1", "tenant-b"),
        };

        var result = filter.Filter(items, i => i.TenantId).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void BelongsToCurrentTenant_MatchingTenant_ReturnsTrue()
    {
        var ctx = new TenantContext { TenantId = "tenant-a" };
        var filter = new TenantDataFilter(ctx);

        filter.BelongsToCurrentTenant("tenant-a").Should().BeTrue();
    }

    [Fact]
    public void BelongsToCurrentTenant_DifferentTenant_ReturnsFalse()
    {
        var ctx = new TenantContext { TenantId = "tenant-a" };
        var filter = new TenantDataFilter(ctx);

        filter.BelongsToCurrentTenant("tenant-b").Should().BeFalse();
    }

    [Fact]
    public void BelongsToCurrentTenant_NullItemTenant_ReturnsTrue()
    {
        var ctx = new TenantContext { TenantId = "tenant-a" };
        var filter = new TenantDataFilter(ctx);

        filter.BelongsToCurrentTenant(null).Should().BeTrue();
    }

    [Fact]
    public void BelongsToCurrentTenant_NoContext_AlwaysTrue()
    {
        var ctx = new TenantContext();
        var filter = new TenantDataFilter(ctx);

        filter.BelongsToCurrentTenant("any-tenant").Should().BeTrue();
    }

    [Fact]
    public void GetTenantStamp_WithTenant_ReturnsTenantId()
    {
        var ctx = new TenantContext { TenantId = "tenant-a" };
        var filter = new TenantDataFilter(ctx);

        filter.GetTenantStamp().Should().Be("tenant-a");
    }

    [Fact]
    public void GetTenantStamp_NoTenant_ReturnsNull()
    {
        var ctx = new TenantContext();
        var filter = new TenantDataFilter(ctx);

        filter.GetTenantStamp().Should().BeNull();
    }

    [Fact]
    public void Filter_CaseInsensitive()
    {
        var ctx = new TenantContext { TenantId = "Tenant-A" };
        var filter = new TenantDataFilter(ctx);

        var items = new[] { new Item("match", "tenant-a") };
        var result = filter.Filter(items, i => i.TenantId).ToList();

        result.Should().HaveCount(1);
    }
}
