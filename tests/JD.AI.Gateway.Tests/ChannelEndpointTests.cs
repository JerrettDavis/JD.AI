using System.Net;

namespace JD.AI.Gateway.Tests;

public sealed class ChannelEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public ChannelEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListChannels_ReturnsConfiguredChannels()
    {
        var response = await _client.GetAsync("/api/channels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // The default config enables "web" channel
        Assert.Contains("web", body);
    }

    [Fact]
    public async Task ConnectChannel_NotFound()
    {
        var response = await _client.PostAsync("/api/channels/nonexistent/connect", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
