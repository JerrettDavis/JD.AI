
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
        // Keep the window long enough that immediate back-to-back calls cannot
        // accidentally cross the boundary under CI load.
        var limiter = new SlidingWindowRateLimiter(maxRequests: 1, window: TimeSpan.FromMilliseconds(250));

        (await limiter.AllowAsync("user1")).Should().BeTrue();
        (await limiter.AllowAsync("user1")).Should().BeFalse();

        // Retry loop avoids flakiness from timer resolution and CI load
        var allowed = false;
        for (var attempt = 0; attempt < 20 && !allowed; attempt++)
        {
            await Task.Delay(100);
            allowed = await limiter.AllowAsync("user1");
        }

        allowed.Should().BeTrue("window should have expired within 2 seconds");
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

    [Fact]
    public async Task CheckAsync_ReturnsRemainingQuota()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 5, window: TimeSpan.FromMinutes(1));

        var result = await limiter.CheckAsync("user1");

        result.Allowed.Should().BeTrue();
        result.Limit.Should().Be(5);
        result.Remaining.Should().Be(4);
        result.ResetsAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CheckAsync_AtLimit_RemainingIsZero()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 2, window: TimeSpan.FromMinutes(1));

        await limiter.CheckAsync("user1");
        await limiter.CheckAsync("user1");
        var result = await limiter.CheckAsync("user1");

        result.Allowed.Should().BeFalse();
        result.Remaining.Should().Be(0);
        result.Limit.Should().Be(2);
    }

    [Fact]
    public async Task CheckAsync_DecreasesRemaining()
    {
        var limiter = new SlidingWindowRateLimiter(maxRequests: 3, window: TimeSpan.FromMinutes(1));

        var r1 = await limiter.CheckAsync("user1");
        var r2 = await limiter.CheckAsync("user1");

        r1.Remaining.Should().Be(2);
        r2.Remaining.Should().Be(1);
    }
}
