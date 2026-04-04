using System.Net;
using System.Net.Http.Json;
using System.Text;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;

namespace JD.AI.Tests.Dashboard;

public sealed class GatewayApiClientTests
{
    [Fact]
    public async Task GetAgentsAsync_ReadsArrayPayload()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse("""[{"id":"a1","provider":"openai","model":"gpt-5","turnCount":2,"createdAt":"2026-03-20T00:00:00Z"}]"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var agents = await client.GetAgentsAsync();

        Assert.Single(agents);
        Assert.Equal("a1", agents[0].Id);
    }

    [Fact]
    public async Task SpawnAgentAsync_PostsAndParsesResponse()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("http://localhost/api/v1/agents", req.RequestUri!.ToString());
            var body = req.Content!.ReadAsStringAsync().Result;
            Assert.DoesNotContain("\"id\":", body, StringComparison.Ordinal);
            Assert.Contains("\"provider\":\"ollama\"", body, StringComparison.Ordinal);
            Assert.Contains("\"model\":\"qwen\"", body, StringComparison.Ordinal);
            return JsonResponse("""{"id":"worker"}""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var created = await client.SpawnAgentAsync(new AgentDefinition { Id = "worker", Provider = "ollama", Model = "qwen" });

        Assert.Equal("worker", created);
    }

    [Fact]
    public async Task DeleteAgentAsync_IssuesDeleteRequest()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Equal("http://localhost/api/v1/agents/abc", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.DeleteAgentAsync("abc");
    }

    [Fact]
    public async Task DeleteAgentAsync_WhenDeleteFails_Throws()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.DeleteAgentAsync("abc"));
    }

    [Fact]
    public async Task GetChannelsAsync_ReadsArrayPayload()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse("""[{"channelType":"discord","displayName":"Discord","isConnected":false}]"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var channels = await client.GetChannelsAsync();

        Assert.Single(channels);
        Assert.Equal("discord", channels[0].Type);
    }

    [Fact]
    public async Task ConnectChannelAsync_PostsConnectEndpoint()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("http://localhost/api/channels/discord/connect", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.ConnectChannelAsync("discord");
    }

    [Fact]
    public async Task DisconnectChannelAsync_PostsDisconnectEndpoint()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("http://localhost/api/channels/discord/disconnect", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.DisconnectChannelAsync("discord");
    }

    [Fact]
    public async Task GetSessionsAsync_UsesLimitQuery()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal("http://localhost/api/sessions?limit=12", req.RequestUri!.ToString());
            return JsonResponse("""[{"id":"s1","agentId":"a1","channel":"discord","updatedAt":"2026-03-20T00:00:00Z"}]""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var sessions = await client.GetSessionsAsync(12);

        Assert.Single(sessions);
        Assert.Equal("s1", sessions[0].Id);
    }

    [Fact]
    public async Task GetSessionAsync_EscapesSessionId()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal("/api/sessions/a%2Fb%20c", req.RequestUri!.AbsolutePath);
            return JsonResponse("""{"id":"a/b c","createdAt":"2026-03-20T00:00:00Z","updatedAt":"2026-03-20T00:00:00Z"}""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var session = await client.GetSessionAsync("a/b c");

        Assert.NotNull(session);
        Assert.Equal("a/b c", session!.Id);
    }

    [Fact]
    public async Task CloseSessionAsync_EscapesSessionId()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/sessions/a%2Fb%20c/close", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.CloseSessionAsync("a/b c");
    }

    [Fact]
    public async Task ExportSessionAsync_EscapesSessionId()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/sessions/a%2Fb%20c/export", req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.ExportSessionAsync("a/b c");
    }

    [Fact]
    public async Task GetProvidersAsync_ReadsArrayPayload()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse("""[{"name":"openai","isConfigured":true,"status":"ready","models":[]}]"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var providers = await client.GetProvidersAsync();

        Assert.Single(providers);
        Assert.Equal("openai", providers[0].Name);
    }

    [Fact]
    public async Task GetProviderModelsAsync_UsesProviderNamePath()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal("http://localhost/api/providers/openai/models", req.RequestUri!.ToString());
            return JsonResponse("""[{"id":"gpt-5.4","displayName":"GPT-5.4","provider":"openai"}]""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var models = await client.GetProviderModelsAsync("openai");

        Assert.Single(models);
        Assert.Equal("gpt-5.4", models[0].Id);
    }

    [Fact]
    public async Task GetRoutingMappingsAsync_MapsDictionaryToArray()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse("""{"discord":"jdai-default","signal":"jdai-research"}"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var mappings = await client.GetRoutingMappingsAsync();

        Assert.Equal(2, mappings.Length);
        Assert.Contains(mappings, m =>
            string.Equals(m.ChannelType, "discord", StringComparison.Ordinal)
            && string.Equals(m.AgentId, "jdai-default", StringComparison.Ordinal));
        Assert.Contains(mappings, m =>
            string.Equals(m.ChannelType, "signal", StringComparison.Ordinal)
            && string.Equals(m.AgentId, "jdai-research", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MapRoutingAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.MapRoutingAsync("discord", "jdai-default"));
    }

    [Fact]
    public async Task GetStatusAsync_ReadsGatewayStatus()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse("""{"status":"running","uptime":"2026-03-20T00:00:00Z","channels":[],"agents":[],"routes":{}}"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var status = await client.GetStatusAsync();

        Assert.NotNull(status);
        Assert.Equal("running", status!.Status);
    }

    [Fact]
    public async Task GetConfigAsync_ReadsRawConfigPayload()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse("""{"server":{"host":"127.0.0.1","port":8080},"channels":[],"agents":[],"providers":[]}"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var config = await client.GetConfigAsync();

        Assert.NotNull(config);
        Assert.Equal(8080, config!.Server.Port);
    }

    [Fact]
    public async Task UpdateServerConfigAsync_PutsConfigAndReturnsBody()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Equal("http://localhost/api/gateway/config/server", req.RequestUri!.ToString());
            return JsonResponse("""{"port":9999,"host":"0.0.0.0","verbose":true}""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var result = await client.UpdateServerConfigAsync(new ServerConfigModel { Port = 9999, Host = "0.0.0.0", Verbose = true });

        Assert.NotNull(result);
        Assert.Equal(9999, result!.Port);
        Assert.Equal("0.0.0.0", result.Host);
        Assert.True(result.Verbose);
    }

    [Fact]
    public async Task UpdateAuthConfigAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateAuthConfigAsync(new AuthConfigModel()));
    }

    [Fact]
    public async Task UpdateRateLimitConfigAsync_PutsAndReturnsBody()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Equal("http://localhost/api/gateway/config/ratelimit", req.RequestUri!.ToString());
            return JsonResponse("""{"enabled":true,"maxRequestsPerMinute":500}""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var result = await client.UpdateRateLimitConfigAsync(new RateLimitConfigModel());

        Assert.NotNull(result);
        Assert.True(result!.Enabled);
        Assert.Equal(500, result.MaxRequestsPerMinute);
    }

    [Fact]
    public async Task UpdateProvidersConfigAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateProvidersConfigAsync([]));
    }

    [Fact]
    public async Task UpdateAgentsConfigAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateAgentsConfigAsync([]));
    }

    [Fact]
    public async Task UpdateChannelsConfigAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateChannelsConfigAsync([]));
    }

    [Fact]
    public async Task UpdateRoutingConfigAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateRoutingConfigAsync(new RoutingConfigModel()));
    }

    [Fact]
    public async Task UpdateOpenClawConfigAsync_ThrowsForFailureStatus()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.UpdateOpenClawConfigAsync(new OpenClawConfigModel()));
    }

    [Fact]
    public async Task GetOpenClawStatusAsync_ReadsObject()
    {
        using var handler = new StubHandler(_ => JsonResponse("""{"enabled":true,"bridgeMode":"passthrough"}"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var result = await client.GetOpenClawStatusAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetOpenClawAgentsAsync_ReadsArray()
    {
        using var handler = new StubHandler(_ => JsonResponse("""[{"id":"jdai-default"}]"""));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var result = await client.GetOpenClawAgentsAsync();

        Assert.NotNull(result);
        Assert.Single(result!);
    }

    [Fact]
    public async Task SyncOpenClawAsync_PostsToSyncEndpoint()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("http://localhost/api/gateway/openclaw/agents/sync", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.SyncOpenClawAsync();
    }

    [Fact]
    public async Task GetAuditEventsAsync_UsesAuditEventsEndpointAndMapsNumericSeverityPayload()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal("http://localhost/api/v1/audit/events?limit=2", req.RequestUri!.ToString());
            return JsonResponse(
                """
                {
                  "totalCount": 1,
                  "count": 1,
                  "events": [
                    {
                      "eventId": "evt-1",
                      "timestamp": "2026-04-04T12:00:00Z",
                      "sessionId": "sess-123",
                      "action": "tool.invoke",
                      "resource": "read_file",
                      "detail": "status=ok; args=path=README.md",
                      "severity": 2,
                      "toolName": "read_file",
                      "toolArguments": "path=README.md"
                    }
                  ]
                }
                """);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var events = await client.GetAuditEventsAsync(2);

        Assert.Single(events);
        Assert.Equal("evt-1", events[0].Id);
        Assert.Equal("warning", events[0].Level);
        Assert.Equal("read_file", events[0].Source);
        Assert.Equal("tool.invoke", events[0].EventType);
        Assert.Equal("status=ok; args=path=README.md", events[0].Message);
        Assert.Contains("\"EventId\": \"evt-1\"", events[0].Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAuditEventsAsync_MapsStringSeverityPayload()
    {
        using var handler = new StubHandler(_ =>
            JsonResponse(
                """
                {
                  "totalCount": 1,
                  "count": 1,
                  "events": [
                    {
                      "eventId": "evt-2",
                      "timestamp": "2026-04-04T12:05:00Z",
                      "userId": "operator",
                      "action": "session.create",
                      "detail": "Session created",
                      "severity": "Info"
                    }
                  ]
                }
                """));
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var events = await client.GetAuditEventsAsync();

        Assert.Single(events);
        Assert.Equal("info", events[0].Level);
        Assert.Equal("operator", events[0].Source);
        Assert.Equal("session.create", events[0].EventType);
    }

    [Fact]
    public async Task GetAuditEventsAsync_AppendsFilterQueryParameters()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(
                "http://localhost/api/v1/audit/events?limit=50&action=tool.invoke&severity=warning&resource=read_file",
                req.RequestUri!.ToString());
            return JsonResponse("""{"totalCount":0,"count":0,"events":[]}""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var events = await client.GetAuditEventsAsync(
            limit: 50,
            action: "tool.invoke",
            severity: "warning",
            resource: "read_file");

        Assert.Empty(events);
    }

    private static HttpClient CreateHttp(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private static HttpResponseMessage JsonResponse(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
