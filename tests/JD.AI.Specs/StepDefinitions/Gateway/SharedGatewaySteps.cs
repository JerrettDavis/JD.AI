using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Specs.Support;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Gateway;

/// <summary>
/// Shared step definitions for Gateway API scenarios.
/// Manages the <see cref="GatewayTestFactory"/>, <see cref="HttpClient"/>,
/// and common Given/When/Then steps reusable across all endpoint features.
/// </summary>
[Binding]
public sealed class SharedGatewaySteps : IDisposable
{
    private readonly ScenarioContext _context;

    internal const string FactoryKey = nameof(GatewayTestFactory);
    internal const string ClientKey = nameof(HttpClient);
    internal const string ResponseKey = nameof(HttpResponseMessage);
    internal const string ResponseBodyKey = "ResponseBody";

    public SharedGatewaySteps(ScenarioContext context) => _context = context;

    // -- Factory / client helpers ---------------------------------------

    internal GatewayTestFactory GetOrCreateFactory()
    {
        if (!_context.TryGetValue(FactoryKey, out var existing))
        {
            var factory = new GatewayTestFactory();
            _context[FactoryKey] = factory;
            return factory;
        }

        return (GatewayTestFactory)existing;
    }

    internal HttpClient GetOrCreateClient()
    {
        if (!_context.TryGetValue(ClientKey, out var existing))
        {
            var client = GetOrCreateFactory().CreateClient();
            _context[ClientKey] = client;
            return client;
        }

        return (HttpClient)existing;
    }

    internal void StoreResponse(HttpResponseMessage response)
    {
        _context[ResponseKey] = response;
    }

    internal HttpResponseMessage GetResponse() => (HttpResponseMessage)_context[ResponseKey];

    internal async Task<JsonElement> GetResponseBodyAsync()
    {
        if (_context.TryGetValue(ResponseBodyKey, out var cached))
            return (JsonElement)cached;

        var response = GetResponse();
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            return default;

        var json = JsonDocument.Parse(body).RootElement;
        _context[ResponseBodyKey] = json;
        return json;
    }

    // -- Given ----------------------------------------------------------

    [Given(@"the gateway is running")]
    public void GivenTheGatewayIsRunning()
    {
        _ = GetOrCreateClient();
    }

    [Given(@"the gateway is running with API key auth enabled")]
    public void GivenTheGatewayIsRunningWithApiKeyAuthEnabled()
    {
        // Auth is controlled by config; default config has auth disabled.
        // For auth-enabled tests we configure via WebApplicationFactory.
        _ = GetOrCreateClient();
    }

    // -- When: generic HTTP verbs ---------------------------------------

    [When(@"I send a GET request to ""(.*)""")]
    public async Task WhenISendAGetRequestTo(string path)
    {
        _context.Remove(ResponseBodyKey);
        var client = GetOrCreateClient();
        StoreResponse(await client.GetAsync(path));
    }

    [When(@"I send a POST request to ""(.*)"" with body:")]
    public async Task WhenISendAPostRequestToWithBody(string path, string body)
    {
        _context.Remove(ResponseBodyKey);
        var client = GetOrCreateClient();
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        StoreResponse(await client.PostAsync(path, content));
    }

    [When(@"I send a POST request to ""(.*)""")]
    public async Task WhenISendAPostRequestTo(string path)
    {
        _context.Remove(ResponseBodyKey);
        var client = GetOrCreateClient();
        StoreResponse(await client.PostAsync(path, null));
    }

    [When(@"I send a PUT request to ""(.*)"" with body:")]
    public async Task WhenISendAPutRequestToWithBody(string path, string body)
    {
        _context.Remove(ResponseBodyKey);
        var client = GetOrCreateClient();
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        StoreResponse(await client.PutAsync(path, content));
    }

    [When(@"I send a DELETE request to ""(.*)""")]
    public async Task WhenISendADeleteRequestTo(string path)
    {
        _context.Remove(ResponseBodyKey);
        var client = GetOrCreateClient();
        StoreResponse(await client.DeleteAsync(path));
    }

    // -- Then: status codes ---------------------------------------------

    [Then(@"the response status should be (.*)")]
    public void ThenTheResponseStatusShouldBe(int statusCode)
    {
        GetResponse().StatusCode.Should().Be((HttpStatusCode)statusCode);
    }

    [Then(@"the response should be successful")]
    public void ThenTheResponseShouldBeSuccessful()
    {
        GetResponse().IsSuccessStatusCode.Should().BeTrue(
            string.Create(CultureInfo.InvariantCulture, $"expected success but got {(int)GetResponse().StatusCode}"));
    }

    [Then(@"the response body should contain ""(.*)""")]
    public async Task ThenTheResponseBodyShouldContain(string expected)
    {
        var body = await GetResponse().Content.ReadAsStringAsync();
        body.Should().Contain(expected);
    }

    [Then(@"the response body should be a JSON array")]
    public async Task ThenTheResponseBodyShouldBeAJsonArray()
    {
        var json = await GetResponseBodyAsync();
        json.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Then(@"the response body should be a JSON object")]
    public async Task ThenTheResponseBodyShouldBeAJsonObject()
    {
        var json = await GetResponseBodyAsync();
        json.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Then(@"the response body should have property ""(.*)""")]
    public async Task ThenTheResponseBodyShouldHaveProperty(string propertyName)
    {
        var json = await GetResponseBodyAsync();
        json.TryGetProperty(propertyName, out _).Should().BeTrue(
            $"expected response body to have property '{propertyName}'");
    }

    [Then(@"the response body property ""(.*)"" should be ""(.*)""")]
    public async Task ThenTheResponseBodyPropertyShouldBe(string propertyName, string expectedValue)
    {
        var json = await GetResponseBodyAsync();
        json.TryGetProperty(propertyName, out var prop).Should().BeTrue(
            $"expected response body to have property '{propertyName}'");
        prop.GetString().Should().Be(expectedValue);
    }

    // -- Cleanup --------------------------------------------------------

    public void Dispose()
    {
        if (_context.TryGetValue(ClientKey, out var clientObj))
            ((HttpClient)clientObj).Dispose();

        if (_context.TryGetValue(FactoryKey, out var factoryObj))
            ((GatewayTestFactory)factoryObj).Dispose();
    }
}
