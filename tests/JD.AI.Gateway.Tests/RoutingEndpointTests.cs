using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Gateway.Endpoints;

namespace JD.AI.Gateway.Tests;

public sealed class RoutingEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public RoutingEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMappings_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/routing/mappings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var mappings = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        mappings.Should().NotBeNull();
    }

    [Fact]
    public async Task PostMap_CreatesMappingAndGetReturnsIt()
    {
        var mapRequest = new MapRequest("channel-1", "agent-1");
        var postResponse = await _client.PostAsJsonAsync("/api/routing/map", mapRequest);

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync("/api/routing/mappings");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var mappings = await getResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        mappings.Should().NotBeNull();
        mappings.Should().ContainKey("channel-1").WhoseValue.Should().Be("agent-1");
    }
}
