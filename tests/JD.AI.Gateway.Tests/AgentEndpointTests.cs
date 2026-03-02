using System.Net;

namespace JD.AI.Gateway.Tests;

public sealed class AgentEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public AgentEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListAgents_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StopAgent_NotFound_Returns204()
    {
        // DELETE always returns 204 even if agent doesn't exist
        var response = await _client.DeleteAsync("/api/agents/nonexistent");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
