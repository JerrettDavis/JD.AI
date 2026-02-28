using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JD.AI.Gateway.Tests;

public sealed class ChannelEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChannelEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListChannels_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/channels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body);
    }

    [Fact]
    public async Task ConnectChannel_NotFound()
    {
        var response = await _client.PostAsync("/api/channels/nonexistent/connect", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
