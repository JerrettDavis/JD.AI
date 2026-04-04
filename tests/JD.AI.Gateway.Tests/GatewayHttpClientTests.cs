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
    private static readonly string[] ExpectedSessionLifecycleUris =
    [
        "https://gateway.test/api/v1/sessions/session-42/close",
        "https://gateway.test/api/v1/sessions/session-42/export"
    ];

    [Fact]
    public async Task GetAgentsAsync_ReturnsAgents()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, new AgentInfo[]
        {
            new AgentInfo(
                "agent-1",
                "openai",
                "gpt-5",
                3,
                DateTimeOffset.Parse("2026-04-03T00:00:00Z", CultureInfo.InvariantCulture))
        }));
        var client = CreateClient(handler);

        var agents = await client.GetAgentsAsync();

        var agent = Assert.Single(agents);
        Assert.Equal("agent-1", agent.Id);
        Assert.Equal("openai", agent.Provider);
        Assert.Equal("gpt-5", agent.Model);
        Assert.Equal(3, agent.TurnCount);
    }

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
            return Json(HttpStatusCode.Created, new
            {
                Id = "agent-123",
            });
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

        Assert.Equal("agent-123", created);
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
    public async Task SendMessageAsync_PostsMessagePayloadAndReturnsRawResponse()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Text(HttpStatusCode.OK, "assistant: acknowledged");
        });
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync("agent-123", "hello there");

        Assert.Equal("assistant: acknowledged", response);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://gateway.test/api/v1/agents/agent-123/message", captured.RequestUri!.ToString());

        var body = await captured.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("hello there", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_WhenGatewayReturnsJsonPayload_ReadsResponseProperty()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Json(HttpStatusCode.OK, new { Response = "assistant: acknowledged" }));
        var client = CreateClient(handler);

        var response = await client.SendMessageAsync("agent-123", "hello there");

        Assert.Equal("assistant: acknowledged", response);
    }

    [Fact]
    public async Task SendMessageAsync_EscapesAgentIdInPath()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Text(HttpStatusCode.OK, "assistant: acknowledged");
        });
        var client = CreateClient(handler);

        await client.SendMessageAsync("agent/123", "hello there");

        Assert.NotNull(captured);
        Assert.Equal("https://gateway.test/api/v1/agents/agent%2F123/message", captured!.RequestUri!.ToString());
    }

    [Fact]
    public async Task DeleteAgentAsync_UsesDeleteEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler);

        await client.DeleteAgentAsync("agent-123");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Delete, captured!.Method);
        Assert.Equal("https://gateway.test/api/v1/agents/agent-123", captured.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetSessionsAsync_UsesLimitAndReturnsSessions()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, new SessionInfo[]
            {
                new SessionInfo
                {
                    Id = "session-42",
                    Name = "Coverage session",
                    ProviderName = "openai",
                    ModelId = "gpt-5",
                    CreatedAt = DateTimeOffset.Parse("2026-04-03T00:00:00Z", CultureInfo.InvariantCulture),
                    UpdatedAt = DateTimeOffset.Parse("2026-04-03T00:05:00Z", CultureInfo.InvariantCulture),
                    TotalTokens = 120,
                    MessageCount = 4,
                    IsActive = true
                }
            });
        });
        var client = CreateClient(handler);

        var sessions = await client.GetSessionsAsync(limit: 12);

        var session = Assert.Single(sessions);
        Assert.Equal("session-42", session.Id);
        Assert.Equal("Coverage session", session.Name);
        Assert.Equal("https://gateway.test/api/v1/sessions?limit=12", captured!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task GetSessionsAsync_WhenLimitIsOutOfRange_Throws(int limit)
    {
        var client = CreateClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("should not send request")));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetSessionsAsync(limit));
    }

    [Fact]
    public async Task GetProviderModelsAsync_EscapesProviderNameInPath()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Json(HttpStatusCode.OK, Array.Empty<ProviderModelInfo>());
        });
        var client = CreateClient(handler);

        _ = await client.GetProviderModelsAsync("OpenAI?x=1");

        Assert.NotNull(captured);
        Assert.Equal("https://gateway.test/api/v1/providers/OpenAI%3Fx%3D1/models", captured!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsSessionWithTurns()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, new SessionInfo
        {
            Id = "session-99",
            Name = "Single session",
            ProviderName = "openai",
            ModelId = "gpt-5",
            CreatedAt = DateTimeOffset.Parse("2026-04-03T01:00:00Z", CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-04-03T01:03:00Z", CultureInfo.InvariantCulture),
            TotalTokens = 90,
            MessageCount = 2,
            IsActive = false,
            Turns =
            [
                new TurnRecord
                {
                    Id = "turn-1",
                    TurnIndex = 1,
                    Role = "assistant",
                    Content = "done",
                    TokensIn = 10,
                    TokensOut = 20,
                    DurationMs = 50,
                    CreatedAt = DateTimeOffset.Parse("2026-04-03T01:01:00Z", CultureInfo.InvariantCulture)
                }
            ]
        }));
        var client = CreateClient(handler);

        var session = await client.GetSessionAsync("session-99");

        Assert.NotNull(session);
        Assert.Equal("session-99", session!.Id);
        Assert.Single(session.Turns);
        Assert.Equal("assistant", session.Turns[0].Role);
    }

    [Fact]
    public async Task CloseAndExportSession_UseExpectedEndpoints()
    {
        var uris = new List<string>();
        var handler = new StubHttpMessageHandler(request =>
        {
            uris.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var client = CreateClient(handler);

        await client.CloseSessionAsync("session-42");
        await client.ExportSessionAsync("session-42");

        Assert.Equal(ExpectedSessionLifecycleUris, uris, StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetProvidersAndProviderModelsAsync_ReturnExpectedResponses()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v1/providers" => Json(HttpStatusCode.OK, new ProviderInfo[]
            {
                new ProviderInfo(
                    "OpenAI",
                    true,
                    null,
                    [new ProviderModelInfo("gpt-5", "GPT-5", "OpenAI")])
            }),
            "/api/v1/providers/OpenAI/models" => Json(HttpStatusCode.OK, new ProviderModelInfo[]
            {
                new ProviderModelInfo("gpt-5-mini", "GPT-5 Mini", "OpenAI")
            }),
            _ => throw new InvalidOperationException($"Unexpected URI: {request.RequestUri}")
        });
        var client = CreateClient(handler);

        var providers = await client.GetProvidersAsync();
        var models = await client.GetProviderModelsAsync("OpenAI");

        var provider = Assert.Single(providers);
        Assert.Equal("OpenAI", provider.Name);
        Assert.True(provider.IsAvailable);

        var model = Assert.Single(models);
        Assert.Equal("gpt-5-mini", model.Id);
        Assert.Equal("OpenAI", model.ProviderName);
    }

    [Fact]
    public async Task GetChannelsAsync_ReturnsChannelStates()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, new ChannelInfo[]
        {
            new ChannelInfo
            {
                ChannelType = "discord",
                DisplayName = "Discord",
                IsConnected = true
            }
        }));
        var client = CreateClient(handler);

        var channels = await client.GetChannelsAsync();

        var channel = Assert.Single(channels);
        Assert.Equal("discord", channel.ChannelType);
        Assert.Equal("Discord", channel.DisplayName);
        Assert.True(channel.IsConnected);
    }

    [Fact]
    public async Task MapRoutingAsync_PostsExpectedPayload()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler);

        await client.MapRoutingAsync("discord:123", "agent-abc");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://gateway.test/api/v1/routing/map", captured.RequestUri!.ToString());

        var body = await captured.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("discord:123", doc.RootElement.GetProperty("channelId").GetString());
        Assert.Equal("agent-abc", doc.RootElement.GetProperty("agentId").GetString());
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatusWithComputedProperties()
    {
        var handler = new StubHttpMessageHandler(_ => Json(HttpStatusCode.OK, new GatewayStatus
        {
            Status = "running",
            Uptime = DateTimeOffset.Parse("2026-04-03T02:00:00Z", CultureInfo.InvariantCulture),
            Channels =
            [
                new GatewayChannelStatus { ChannelType = "discord", DisplayName = "Discord", IsConnected = true },
                new GatewayChannelStatus { ChannelType = "slack", DisplayName = "Slack", IsConnected = false }
            ],
            Agents =
            [
                new GatewayAgentStatus { Id = "agent-1", Provider = "openai", Model = "gpt-5" },
                new GatewayAgentStatus { Id = "agent-2", Provider = "anthropic", Model = "claude-sonnet-4.6" }
            ],
            Routes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["discord:123"] = "agent-1"
            }
        }));
        var client = CreateClient(handler);

        var status = await client.GetStatusAsync();

        Assert.NotNull(status);
        Assert.True(status!.IsRunning);
        Assert.Equal(2, status.ActiveAgents);
        Assert.Equal(1, status.ActiveChannels);
        Assert.Equal("agent-1", status.Routes["discord:123"]);
    }

    [Fact]
    public async Task IndexDocumentAsync_PostsExpectedPayload()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var client = CreateClient(handler);

        await client.IndexDocumentAsync("coverage notes", source: "README.md");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://gateway.test/api/v1/memory/index", captured.RequestUri!.ToString());

        var body = await captured.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("coverage notes", doc.RootElement.GetProperty("content").GetString());
        Assert.Equal("README.md", doc.RootElement.GetProperty("source").GetString());
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

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrueWhenGatewayStatusEndpointSucceeds()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        var healthy = await client.IsHealthyAsync();

        Assert.True(healthy);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new StubHttpMessageHandler(_ => throw new OperationCanceledException(cts.Token));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.IsHealthyAsync(cts.Token));
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

    private static HttpResponseMessage Text(HttpStatusCode statusCode, string payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/plain")
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
