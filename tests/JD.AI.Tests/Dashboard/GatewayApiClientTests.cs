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
            Assert.Equal("http://localhost/api/agents", req.RequestUri!.ToString());
            return JsonResponse("""{"id":"worker","provider":"ollama","model":"qwen","turnCount":0,"createdAt":"2026-03-20T00:00:00Z"}""");
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        var created = await client.SpawnAgentAsync(new AgentDefinition { Id = "worker", Provider = "ollama", Model = "qwen" });

        Assert.NotNull(created);
        Assert.Equal("worker", created!.Id);
    }

    [Fact]
    public async Task DeleteAgentAsync_IssuesDeleteRequest()
    {
        using var handler = new StubHandler(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Equal("http://localhost/api/agents/abc", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = CreateHttp(handler);
        var client = new GatewayApiClient(http);

        await client.DeleteAgentAsync("abc");
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
