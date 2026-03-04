using System.Net;
using System.Net.Http.Json;

namespace JD.AI.Gateway.Tests;

public sealed class PluginEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public PluginEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListPlugins_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/plugins");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InstallPlugin_WithoutSource_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/plugins/install", new { source = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePlugin_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PostAsync("/api/plugins/not-installed/update", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
