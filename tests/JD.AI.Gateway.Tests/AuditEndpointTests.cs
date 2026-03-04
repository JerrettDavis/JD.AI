using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JD.AI.Gateway.Tests;

public sealed class AuditEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public AuditEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QueryAuditEvents_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/audit/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("totalCount", out _));
        Assert.True(json.TryGetProperty("count", out _));
        Assert.True(json.TryGetProperty("events", out _));
    }

    [Fact]
    public async Task QueryAuditEvents_WithFilters_ReturnsOk()
    {
        var response = await _client.GetAsync(
            "/api/audit/events?action=tool.invoke&severity=warning&limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditEvent_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/audit/events/nonexistent-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditStats_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/audit/stats");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("totalEvents", out _));
        Assert.True(json.TryGetProperty("bySeverity", out _));
        Assert.True(json.TryGetProperty("topActions", out _));
    }
}
