
using FluentAssertions;
using JD.AI.Core.Security;
using Xunit;

namespace JD.AI.Tests.Security;

public class SlidingWindowRateLimiterTests
{
    [Fact]
    public async Task Allow_UnderLimit_ReturnsTrue()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 5, window: TimeSpan.FromMinutes(1));

        var allowed = await limiter.AllowAsync("user1");

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Allow_AtLimit_ReturnsFalse()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 3, window: TimeSpan.FromMinutes(1));

        for (var i = 0; i < 3; i++)
            (await limiter.AllowAsync("user1")).Should().BeTrue();

        var blocked = await limiter.AllowAsync("user1");

        blocked.Should().BeFalse();
    }

    [Fact]
    public async Task Allow_AfterWindowExpires_AllowsAgain()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 1, window: TimeSpan.FromMilliseconds(100));

        (await limiter.AllowAsync("user1")).Should().BeTrue();
        (await limiter.AllowAsync("user1")).Should().BeFalse();

        // Generous delay to avoid flakiness under parallel test load
        await Task.Delay(1000);

        (await limiter.AllowAsync("user1")).Should().BeTrue();
    }

    [Fact]
    public async Task Allow_DifferentKeys_IndependentLimits()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 1, window: TimeSpan.FromMinutes(1));

        (await limiter.AllowAsync("user1")).Should().BeTrue();
        (await limiter.AllowAsync("user1")).Should().BeFalse();

        // Different key should still be allowed
        (await limiter.AllowAsync("user2")).Should().BeTrue();
    }

    [Fact]
    public async Task Allow_DefaultConfig_60PerMinute()
    {
        var limiter = new SlidingWindowRateLimiter();

        for (var i = 0; i < 60; i++)
            (await limiter.AllowAsync("user1")).Should().BeTrue($"request {i + 1} of 60 should be allowed");

        (await limiter.AllowAsync("user1")).Should().BeFalse("61st request should be blocked");
    }
}
