using System.Net;
using System.Net.Http.Json;

namespace JD.AI.Gateway.Tests;

public sealed class ProviderEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public ProviderEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListProviders_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/providers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProviderModels_Unknown_Returns404()
    {
        var response = await _client.GetAsync("/api/providers/nonexistent/models");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
