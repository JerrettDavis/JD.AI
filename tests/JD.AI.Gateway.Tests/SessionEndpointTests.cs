using System.Net;
using System.Net.Http.Json;

namespace JD.AI.Gateway.Tests;

public sealed class SessionEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public SessionEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListSessions_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/sessions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
