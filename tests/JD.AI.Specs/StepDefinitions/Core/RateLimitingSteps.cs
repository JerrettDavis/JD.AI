using FluentAssertions;
using JD.AI.Core.Security;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class RateLimitingSteps
{
    private readonly ScenarioContext _context;

    public RateLimitingSteps(ScenarioContext context) => _context = context;

    [Given(@"a rate limiter allowing (\d+) requests per minute")]
    public void GivenARateLimiterAllowingRequestsPerMinute(int maxRequests)
    {
        _context.Set(new SlidingWindowRateLimiter(maxRequests, TimeSpan.FromMinutes(1)), "RateLimiter");
    }

    [Given(@"a rate limiter allowing (\d+) requests per (\d+) second window")]
    public void GivenARateLimiterAllowingRequestsPerSecondWindow(int maxRequests, int seconds)
    {
        _context.Set(new SlidingWindowRateLimiter(maxRequests, TimeSpan.FromSeconds(seconds)), "RateLimiter");
    }

    [When(@"I make (\d+) requests? for key ""(.*)""")]
    public async Task WhenIMakeRequestsForKey(int count, string key)
    {
        var limiter = _context.Get<SlidingWindowRateLimiter>("RateLimiter");
        var results = new List<bool>();
        for (var i = 0; i < count; i++)
        {
            results.Add(await limiter.AllowAsync(key));
        }

        if (!_context.TryGetValue("RateResults", out List<bool>? existing) || existing is null)
        {
            existing = [];
            _context.Set(existing, "RateResults");
        }
        existing.AddRange(results);
        _context.Set(results.Last(), "LastRequestResult");
    }

    [When(@"I wait for the rate limit window to expire")]
    public async Task WhenIWaitForTheRateLimitWindowToExpire()
    {
        // Wait slightly longer than the window to ensure expiry
        await Task.Delay(TimeSpan.FromSeconds(1.5));
    }

    [Then(@"all requests should be allowed")]
    public void ThenAllRequestsShouldBeAllowed()
    {
        var results = _context.Get<List<bool>>("RateResults");
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Then(@"(\d+) requests should be allowed")]
    public void ThenNRequestsShouldBeAllowed(int count)
    {
        var results = _context.Get<List<bool>>("RateResults");
        results.Count(r => r).Should().Be(count);
    }

    [Then(@"(\d+) requests should be blocked")]
    public void ThenNRequestsShouldBeBlocked(int count)
    {
        var results = _context.Get<List<bool>>("RateResults");
        results.Count(r => !r).Should().Be(count);
    }

    [Then(@"the last request should be allowed")]
    public void ThenTheLastRequestShouldBeAllowed()
    {
        var result = _context.Get<bool>("LastRequestResult");
        result.Should().BeTrue();
    }
}
