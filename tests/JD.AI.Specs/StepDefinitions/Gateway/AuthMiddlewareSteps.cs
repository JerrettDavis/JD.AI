using System.Net;
using FluentAssertions;
using JD.AI.Core.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

[Binding]
public sealed class AuthMiddlewareSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private WebApplicationFactory<Program>? _authFactory;
    private HttpClient? _authClient;
    private HttpResponseMessage? _response;

    public AuthMiddlewareSteps(ScenarioContext context) => _context = context;

    [Given(@"the gateway is running with auth enabled and key ""(.*)""")]
    public void GivenTheGatewayIsRunningWithAuthEnabledAndKey(string apiKey)
    {
        // Override configuration at the IConfiguration level so that Program.cs
        // reads Auth.Enabled = true and registers ApiKeyAuthMiddleware.
        _authFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Gateway:Auth:Enabled", "true");
                builder.UseSetting("Gateway:Auth:ApiKeys:0:Key", apiKey);
                builder.UseSetting("Gateway:Auth:ApiKeys:0:Name", "TestKey");
                builder.UseSetting("Gateway:Auth:ApiKeys:0:Role", "Admin");
            });
        _authClient = _authFactory.CreateClient();
        _context["AuthApiKey"] = apiKey;
    }

    [When(@"I send an unauthenticated GET request to ""(.*)""")]
    public async Task WhenISendAnUnauthenticatedGetRequestTo(string path)
    {
        _response = await _authClient!.GetAsync(path);
    }

    [When(@"I send an authenticated GET request to ""(.*)"" with API key ""(.*)""")]
    public async Task WhenISendAnAuthenticatedGetRequestToWithApiKey(string path, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-API-Key", apiKey);
        _response = await _authClient!.SendAsync(request);
    }

    // Scoped to @auth tag so it does not conflict with SharedGatewaySteps
    [Then(@"the response status should be (.*)")]
    [Scope(Tag = "auth")]
    public void ThenTheResponseStatusShouldBeAuth(int statusCode)
    {
        _response!.StatusCode.Should().Be((HttpStatusCode)statusCode);
    }

    public void Dispose()
    {
        _authClient?.Dispose();
        _authFactory?.Dispose();
    }
}
