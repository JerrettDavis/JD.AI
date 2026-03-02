using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Gateway.Config;

namespace JD.AI.Gateway.Tests;

public sealed class GatewayConfigEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public GatewayConfigEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConfig_ReturnsOkWithStructure()
    {
        var response = await _client.GetAsync("/api/gateway/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("server");
        body.Should().Contain("channels");
        body.Should().Contain("agents");
        body.Should().Contain("routing");
        body.Should().Contain("openClaw");
    }

    [Fact]
    public async Task GetConfig_RedactsSecrets()
    {
        var response = await _client.GetAsync("/api/gateway/config");
        var body = await response.Content.ReadAsStringAsync();

        // Env-ref tokens should remain visible as references, but not expose actual values
        // Settings that start with "env:" keep their reference form
        body.Should().NotContain("actual-secret-value");
    }

    [Fact]
    public async Task GetStatus_ReturnsRunningStatus()
    {
        var response = await _client.GetAsync("/api/gateway/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("running");
    }

    [Fact]
    public async Task GetStatus_IncludesChannelsAgentsRoutes()
    {
        var response = await _client.GetAsync("/api/gateway/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("channels", out _).Should().BeTrue();
        json.TryGetProperty("agents", out _).Should().BeTrue();
        json.TryGetProperty("routes", out _).Should().BeTrue();
    }
}
