using System.Net;
using System.Net.Http.Json;
using JD.AI.Core.Infrastructure;

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
        var response = await _client.GetAsync(GatewayRuntimeDefaults.HealthPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        var response = await _client.GetAsync(GatewayRuntimeDefaults.ReadyPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReadyResponse>();
        Assert.Equal("Ready", body?.Status);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk()
    {
        var response = await _client.GetAsync(GatewayRuntimeDefaults.HealthReadyPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var response = await _client.GetAsync(GatewayRuntimeDefaults.HealthLivePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LiveResponse>();
        Assert.Equal("Live", body?.Status);
    }

    private sealed record ReadyResponse(string Status);

    private sealed record LiveResponse(string Status);
}
