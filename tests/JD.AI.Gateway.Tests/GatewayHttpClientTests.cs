using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using JD.AI.Gateway.Client;
using JD.AI.Gateway.Client.Models;

namespace JD.AI.Gateway.Tests;

public sealed class GatewayHttpClientTests
{
    private static readonly string[] ExpectedMemorySearchResults = ["memory-a", "memory-b"];
    private static readonly string[] ExpectedChannelLifecycleUris =
    [
        "https://gateway.test/api/v1/channels/discord/connect",
        "https://gateway.test/api/v1/channels/discord/disconnect"
    ];

    [Fact]
    public async Task GetRoutingMappingsAsync_MapsDictionaryResponseIntoRecords()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["discord:123"] = "agent-a",
            ["telegram:456"] = "agent-b"
        }));
        var client = CreateClient(handler);

        var mappings = await client.GetRoutingMappingsAsync();

        Assert.Equal(2, mappings.Length);
        Assert.Contains(mappings, m => string.Equals(m.ChannelId, "discord:123", StringComparison.Ordinal)
            && string.Equals(m.AgentId, "agent-a", StringComparison.Ordinal));
        Assert.Contains(mappings, m => string.Equals(m.ChannelId, "telegram:456", StringComparison.Ordinal)
            && string.Equals(m.AgentId, "agent-b", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchMemoryAsync_PostsExpectedPayloadAndReturnsValues()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, ExpectedMemorySearchResults);
        });
        var client = CreateClient(handler);

        var results = await client.SearchMemoryAsync("queue bug", limit: 7);

        Assert.Equal(ExpectedMemorySearchResults, results, StringComparer.Ordinal);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://gateway.test/api/v1/memory/search", captured.RequestUri!.ToString());

        var body = await captured.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("queue bug", doc.RootElement.GetProperty("query").GetString());
        Assert.Equal(7, doc.RootElement.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task SpawnAgentAsync_PostsDefinitionAndParsesCreatedAgent()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, new AgentInfo(
                "agent-123",
                "openai",
                "gpt-test",
                0,
                DateTimeOffset.Parse("2026-04-02T00:00:00Z", CultureInfo.InvariantCulture)));
        });
        var client = CreateClient(handler);
        var definition = new AgentDefinition
        {
            Id = "agent-123",
            Provider = "openai",
            Model = "gpt-test",
            SystemPrompt = "Be useful",
            AutoSpawn = true,
            MaxTurns = 5,
            Tools = ["read", "write"]
        };

        var created = await client.SpawnAgentAsync(definition);

        Assert.NotNull(created);
        Assert.Equal("agent-123", created!.Id);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://gateway.test/api/v1/agents", captured.RequestUri!.ToString());

        var body = await captured.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("agent-123", doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("openai", doc.RootElement.GetProperty("provider").GetString());
        Assert.Equal("gpt-test", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseWhenRequestThrows()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));
        var client = CreateClient(handler);

        var healthy = await client.IsHealthyAsync();

        Assert.False(healthy);
    }

    [Fact]
    public async Task ConnectAndDisconnectChannel_UseExpectedEndpoints()
    {
        var uris = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            uris.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler);

        await client.ConnectChannelAsync("discord");
        await client.DisconnectChannelAsync("discord");

        Assert.Equal(ExpectedChannelLifecycleUris, uris, StringComparer.Ordinal);
    }

    private static GatewayHttpClient CreateClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://gateway.test/")
        };

        return new GatewayHttpClient(http);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode statusCode, T payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
