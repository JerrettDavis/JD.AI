using FluentAssertions;
using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantContextTests
{
    [Fact]
    public void Default_TenantId_IsNull()
    {
        var ctx = new TenantContext();
        ctx.TenantId.Should().BeNull();
        ctx.TenantName.Should().BeNull();
    }

    [Fact]
    public void IsResolved_NullTenantId_ReturnsFalse()
    {
        var ctx = new TenantContext { TenantId = null };
        ctx.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void IsResolved_EmptyTenantId_ReturnsFalse()
    {
        var ctx = new TenantContext { TenantId = "" };
        ctx.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void IsResolved_WhitespaceTenantId_ReturnsFalse()
    {
        var ctx = new TenantContext { TenantId = "   " };
        ctx.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void IsResolved_ValidTenantId_ReturnsTrue()
    {
        var ctx = new TenantContext { TenantId = "tenant-abc" };
        ctx.IsResolved.Should().BeTrue();
    }

    [Fact]
    public void SetProperties_Roundtrip()
    {
        var ctx = new TenantContext
        {
            TenantId = "org-123",
            TenantName = "Acme Corp",
        };
        ctx.TenantId.Should().Be("org-123");
        ctx.TenantName.Should().Be("Acme Corp");
        ctx.IsResolved.Should().BeTrue();
    }
}
