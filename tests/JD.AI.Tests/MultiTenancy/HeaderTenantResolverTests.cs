using FluentAssertions;
using JD.AI.Core.MultiTenancy;

namespace JD.AI.Tests.MultiTenancy;

public sealed class HeaderTenantResolverTests
{
    [Fact]
    public async Task ResolveAsync_WithTenantHeader_ReturnsTenantInfo()
    {
        var resolver = new HeaderTenantResolver();
        var context = new TenantResolutionContext
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HeaderTenantResolver.TenantHeader] = "tenant-abc",
            },
        };

        var result = await resolver.ResolveAsync(context);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be("tenant-abc");
    }

    [Fact]
    public async Task ResolveAsync_NoHeader_ReturnsNull()
    {
        var resolver = new HeaderTenantResolver();
        var context = new TenantResolutionContext();

        var result = await resolver.ResolveAsync(context);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoHeader_WithDefault_ReturnsDefault()
    {
        var resolver = new HeaderTenantResolver(defaultTenantId: "default-tenant");
        var context = new TenantResolutionContext();

        var result = await resolver.ResolveAsync(context);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be("default-tenant");
    }

    [Fact]
    public async Task ResolveAsync_EmptyHeader_ReturnsNull()
    {
        var resolver = new HeaderTenantResolver();
        var context = new TenantResolutionContext
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HeaderTenantResolver.TenantHeader] = "  ",
            },
        };

        var result = await resolver.ResolveAsync(context);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_TrimsWhitespace()
    {
        var resolver = new HeaderTenantResolver();
        var context = new TenantResolutionContext
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HeaderTenantResolver.TenantHeader] = "  tenant-123  ",
            },
        };

        var result = await resolver.ResolveAsync(context);

        result!.TenantId.Should().Be("tenant-123");
    }
}
