using FluentAssertions;
using JD.AI.Core.Security;
using JD.AI.Gateway.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Gateway.Tests.Middleware;

public sealed class RequireRoleFilterTests
{
    // ── RequireRoleAttribute ──────────────────────────────────────────────

    [Fact]
    public void Attribute_StoresMinimumRole()
    {
        var attr = new RequireRoleAttribute(GatewayRole.Admin);
        attr.MinimumRole.Should().Be(GatewayRole.Admin);
    }

    [Theory]
    [InlineData(GatewayRole.Guest)]
    [InlineData(GatewayRole.User)]
    [InlineData(GatewayRole.Operator)]
    [InlineData(GatewayRole.Admin)]
    public void Attribute_AllRoles(GatewayRole role)
    {
        var attr = new RequireRoleAttribute(role);
        attr.MinimumRole.Should().Be(role);
    }

    // ── GatewayRole enum ──────────────────────────────────────────────────

    [Theory]
    [InlineData(GatewayRole.Guest, 0)]
    [InlineData(GatewayRole.User, 10)]
    [InlineData(GatewayRole.Operator, 50)]
    [InlineData(GatewayRole.Admin, 100)]
    public void GatewayRole_Values(GatewayRole role, int expected) =>
        ((int)role).Should().Be(expected);

    [Fact]
    public void GatewayRole_Ordering_AdminHighest()
    {
        ((int)GatewayRole.Admin).Should().BeGreaterThan((int)GatewayRole.Operator);
        ((int)GatewayRole.Operator).Should().BeGreaterThan((int)GatewayRole.User);
        ((int)GatewayRole.User).Should().BeGreaterThan((int)GatewayRole.Guest);
    }

    // ── GatewayIdentity record ────────────────────────────────────────────

    [Fact]
    public void GatewayIdentity_Construction()
    {
        var now = DateTimeOffset.UtcNow;
        var identity = new GatewayIdentity("id-1", "Alice", GatewayRole.Operator, now);

        identity.Id.Should().Be("id-1");
        identity.DisplayName.Should().Be("Alice");
        identity.Role.Should().Be(GatewayRole.Operator);
        identity.AuthenticatedAt.Should().Be(now);
        identity.Claims.Should().BeEmpty();
    }

    [Fact]
    public void GatewayIdentity_WithClaims()
    {
        var identity = new GatewayIdentity("id-1", "Bob", GatewayRole.User, DateTimeOffset.UtcNow)
        {
            Claims = new Dictionary<string, string>(StringComparer.Ordinal) { ["tenant"] = "acme" },
        };

        identity.Claims.Should().ContainKey("tenant");
        identity.Claims["tenant"].Should().Be("acme");
    }

    [Fact]
    public void GatewayIdentity_RecordEquality()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new GatewayIdentity("x", "X", GatewayRole.Admin, now);
        var b = new GatewayIdentity("x", "X", GatewayRole.Admin, now);
        // Claims is a Dictionary (reference equality), so use positional equality check
        a.Id.Should().Be(b.Id);
        a.DisplayName.Should().Be(b.DisplayName);
        a.Role.Should().Be(b.Role);
        a.AuthenticatedAt.Should().Be(b.AuthenticatedAt);
    }

    // ── RateLimitResult record ────────────────────────────────────────────

    [Fact]
    public void RateLimitResult_Construction()
    {
        var reset = DateTimeOffset.UtcNow.AddMinutes(1);
        var result = new RateLimitResult(true, 60, 58, reset);

        result.Allowed.Should().BeTrue();
        result.Limit.Should().Be(60);
        result.Remaining.Should().Be(58);
        result.ResetsAt.Should().Be(reset);
    }

    [Fact]
    public void RateLimitResult_Denied()
    {
        var reset = DateTimeOffset.UtcNow.AddSeconds(30);
        var result = new RateLimitResult(false, 60, 0, reset);

        result.Allowed.Should().BeFalse();
        result.Remaining.Should().Be(0);
    }

    [Theory]
    [InlineData(GatewayRole.Guest, GatewayRole.Guest)]
    [InlineData(GatewayRole.User, GatewayRole.Guest)]
    [InlineData(GatewayRole.Operator, GatewayRole.User)]
    [InlineData(GatewayRole.Admin, GatewayRole.Operator)]
    public async Task RequireRoleFilter_AllowsIdentityMeetingMinimumRole(
        GatewayRole actualRole,
        GatewayRole minimumRole)
    {
        var context = CreateInvocationContext();
        context.HttpContext.Items["Identity"] = new GatewayIdentity("user-1", "User", actualRole, DateTimeOffset.UtcNow);

        var nextCalled = false;
        var expected = TypedResults.Ok();

        var result = await new RequireRoleFilter(minimumRole).InvokeAsync(
            context,
            _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>(expected);
            });

        nextCalled.Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task RequireRoleFilter_DeniesIdentityBelowMinimumRole()
    {
        var context = CreateInvocationContext();
        context.HttpContext.Items["Identity"] = new GatewayIdentity("user-1", "User", GatewayRole.User, DateTimeOffset.UtcNow);

        var result = await new RequireRoleFilter(GatewayRole.Operator).InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(TypedResults.Ok()));

        await AssertForbiddenAsync(result, context.HttpContext);
    }

    [Fact]
    public async Task RequireRoleFilter_DeniesWhenIdentityMissing()
    {
        var context = CreateInvocationContext();

        var result = await new RequireRoleFilter(GatewayRole.Guest).InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(TypedResults.Ok()));

        await AssertForbiddenAsync(result, context.HttpContext);
    }

    [Fact]
    public async Task RequireRoleFilter_DeniesWhenIdentityHasWrongType()
    {
        var context = CreateInvocationContext();
        context.HttpContext.Items["Identity"] = "not-an-identity";

        var result = await new RequireRoleFilter(GatewayRole.Guest).InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(TypedResults.Ok()));

        await AssertForbiddenAsync(result, context.HttpContext);
    }

    [Theory]
    [InlineData(GatewayRole.Guest)]
    [InlineData(GatewayRole.User)]
    [InlineData(GatewayRole.Operator)]
    [InlineData(GatewayRole.Admin)]
    public void RequireRole_AddsConventionAndReturnsBuilder(GatewayRole minimumRole)
    {
        var builder = new TestEndpointConventionBuilder();

        var returned = builder.RequireRole(minimumRole);

        returned.Should().BeSameAs(builder);
        builder.Conventions.Should().HaveCount(1);
    }

    private static TestEndpointFilterInvocationContext CreateInvocationContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<JsonOptions>(_ => { });
        httpContext.RequestServices = services.BuildServiceProvider();
        return new TestEndpointFilterInvocationContext(httpContext);
    }

    private static async Task AssertForbiddenAsync(object? result, HttpContext httpContext)
    {
        var typedResult = result.Should().BeAssignableTo<IResult>().Subject;
        await typedResult.ExecuteAsync(httpContext);
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private sealed class TestEndpointFilterInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;

        public override IList<object?> Arguments { get; } = [];

        public override T GetArgument<T>(int index) => (T)Arguments[index]!;
    }

    private sealed class TestEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);
    }
}
