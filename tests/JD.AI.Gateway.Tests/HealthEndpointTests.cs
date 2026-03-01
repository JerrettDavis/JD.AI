using System.Net;
using System.Net.Http.Json;

namespace JD.AI.Gateway.Tests;

public sealed class HealthEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        var response = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReadyResponse>();
        Assert.Equal("Ready", body?.Status);
    }

    private sealed record ReadyResponse(string Status);
}
