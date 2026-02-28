using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Gateway.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JD.AI.Gateway.Tests;

public sealed class RoutingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RoutingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMappings_ReturnsEmptyInitially()
    {
        var response = await _client.GetAsync("/api/routing/mappings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("{}");
    }

    [Fact]
    public async Task PostMap_CreatesMappingAndGetReturnsIt()
    {
        // Use a unique factory instance to avoid shared state with other tests
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var mapRequest = new MapRequest("channel-1", "agent-1");
        var postResponse = await client.PostAsJsonAsync("/api/routing/map", mapRequest);

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync("/api/routing/mappings");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var mappings = await getResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        mappings.Should().NotBeNull();
        mappings.Should().ContainKey("channel-1").WhoseValue.Should().Be("agent-1");
    }
}
