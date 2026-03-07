using System.Net;
using FluentAssertions;
using JD.AI.Core.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

[Binding]
public sealed class RateLimitMiddlewareSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private readonly List<HttpResponseMessage> _responses = [];

    public RateLimitMiddlewareSteps(ScenarioContext context) => _context = context;

    [Given(@"the gateway is running with rate limiting enabled at (\d+) requests per minute")]
    public void GivenTheGatewayIsRunningWithRateLimitingEnabledAtRequestsPerMinute(int maxRequests)
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Replace the rate limiter with one that uses the test limit
                    services.AddSingleton<IRateLimiter>(
                        new SlidingWindowRateLimiter(maxRequests));
                });
            });
        _client = _factory.CreateClient();
    }

    [When(@"I send (\d+) GET requests to ""(.*)""")]
    public async Task WhenISendGetRequestsTo(int count, string path)
    {
        _responses.Clear();
        for (var i = 0; i < count; i++)
        {
            _responses.Add(await _client!.GetAsync(path));
        }
    }

    [Then(@"all responses should have status (\d+)")]
    public void ThenAllResponsesShouldHaveStatus(int statusCode)
    {
        var expected = (HttpStatusCode)statusCode;
        _responses.Should().AllSatisfy(r =>
            r.StatusCode.Should().Be(expected));
    }

    [Then(@"at least one response should have status (\d+)")]
    public void ThenAtLeastOneResponseShouldHaveStatus(int statusCode)
    {
        var expected = (HttpStatusCode)statusCode;
        _responses.Should().Contain(r => r.StatusCode == expected,
            $"expected at least one response with status {statusCode}");
    }

    public void Dispose()
    {
        foreach (var response in _responses)
            response.Dispose();

        _client?.Dispose();
        _factory?.Dispose();
    }
}
