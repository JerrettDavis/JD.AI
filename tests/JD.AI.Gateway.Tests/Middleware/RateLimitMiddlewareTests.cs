using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Security;
using JD.AI.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Middleware;

public sealed class RateLimitMiddlewareTests
{
    [Theory]
    [InlineData(GatewayRuntimeDefaults.HealthPath)]
    [InlineData(GatewayRuntimeDefaults.HealthReadyPath)]
    [InlineData(GatewayRuntimeDefaults.HealthLivePath)]
    [InlineData(GatewayRuntimeDefaults.HealthStartupPath)]
    [InlineData(GatewayRuntimeDefaults.ReadyPath)]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_content/site.css")]
    [InlineData("/css/site")]
    [InlineData("/js/app")]
    [InlineData("/index.html")]
    [InlineData("/appsettings.json")]
    [InlineData("/favicon.png")]
    [InlineData("/icon-192.png")]
    [InlineData("/")]
    public async Task InvokeAsync_BypassPaths_SkipRateLimiting(string path)
    {
        var limiter = Substitute.For<IRateLimiter>();
        var nextCalled = false;
        var middleware = new RateLimitMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            limiter);
        var context = CreateContext(path);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        await limiter.DidNotReceiveWithAnyArgs().CheckAsync(default!, default);
    }

    [Theory]
    [InlineData("/api/report.json")]
    [InlineData("/api/script.js")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/assets/app.wasm")]
    [InlineData("/manifest.json")]
    [InlineData("/favicon.ico")]
    [InlineData("/images/logo.png")]
    [InlineData("/images/logo.svg")]
    [InlineData("/healthcheck")]
    [InlineData("/readyz")]
    public async Task InvokeAsync_NonBypassPaths_StillApplyRateLimiting(string path)
    {
        var limiter = Substitute.For<IRateLimiter>();
        limiter.CheckAsync("127.0.0.1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RateLimitResult(true, 60, 59, DateTimeOffset.UtcNow.AddMinutes(1))));

        var middleware = new RateLimitMiddleware(_ => Task.CompletedTask, limiter);
        var context = CreateContext(path);

        await middleware.InvokeAsync(context);

        await limiter.Received(1).CheckAsync("127.0.0.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_AllowedRequest_UsesRemoteIpAndSetsRateLimitHeaders()
    {
        var resetAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var limiter = Substitute.For<IRateLimiter>();
        limiter.CheckAsync("203.0.113.7", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RateLimitResult(true, 60, 59, resetAt)));

        var nextCalled = false;
        var middleware = new RateLimitMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            limiter);
        var context = CreateContext("/api/sessions", IPAddress.Parse("203.0.113.7"));

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("60");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("59");
        context.Response.Headers["X-RateLimit-Reset"].ToString().Should().Be(resetAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        await limiter.Received(1).CheckAsync("203.0.113.7", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_DeniedRequest_PrefersIdentityKeyAndReturns429()
    {
        var resetAt = DateTimeOffset.UtcNow.AddSeconds(20);
        var limiter = Substitute.For<IRateLimiter>();
        limiter.CheckAsync("identity-123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RateLimitResult(false, 60, 0, resetAt)));

        var nextCalled = false;
        var middleware = new RateLimitMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            limiter);
        var context = CreateContext("/api/agents", IPAddress.Parse("203.0.113.7"));
        context.Items["Identity"] = new GatewayIdentity("identity-123", "Alice", GatewayRole.User, DateTimeOffset.UtcNow);
        var before = DateTimeOffset.UtcNow;

        await middleware.InvokeAsync(context);
        var after = DateTimeOffset.UtcNow;

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("60");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
        var retryAfter = int.Parse(context.Response.Headers["Retry-After"].ToString(), CultureInfo.InvariantCulture);
        var minExpected = Math.Max(1, (int)Math.Floor((resetAt - after).TotalSeconds));
        var maxExpected = Math.Max(1, (int)Math.Ceiling((resetAt - before).TotalSeconds));
        retryAfter.Should().BeInRange(minExpected, maxExpected);
        (await ReadResponseBodyAsync(context)).Should().Contain("Too Many Requests");
        await limiter.Received(1).CheckAsync("identity-123", Arg.Any<CancellationToken>());
        await limiter.DidNotReceive().CheckAsync("203.0.113.7", Arg.Any<CancellationToken>());
    }

    private static DefaultHttpContext CreateContext(string path, IPAddress? remoteIpAddress = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = remoteIpAddress ?? IPAddress.Parse("127.0.0.1");
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
