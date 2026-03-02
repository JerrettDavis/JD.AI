using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace JD.AI.Gateway.Tests;

/// <summary>
/// Integration tests for the AgentHub.StreamChat SignalR method.
/// </summary>
public sealed class AgentHubStreamChatTests : IClassFixture<GatewayTestFactory>
{
    private readonly GatewayTestFactory _factory;
    private readonly HttpClient _client;

    public AgentHubStreamChatTests(GatewayTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AgentHub_NegotiateEndpoint_Returns200()
    {
        var response = await _client.PostAsync("/hubs/agent/negotiate?negotiateVersion=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("connectionId");
    }

    [Fact]
    public async Task AgentHub_StreamChat_WithInvalidAgent_ReturnsError()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl($"{_client.BaseAddress}hubs/agent", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await hub.StartAsync();
        hub.State.Should().Be(HubConnectionState.Connected);

        var chunks = new List<(string Type, string? Content)>();
        var stream = hub.StreamAsync<AgentStreamChunkDto>("StreamChat", "nonexistent-agent", "hello");
        await foreach (var chunk in stream)
        {
            chunks.Add((chunk.Type, chunk.Content));
        }

        chunks.Should().Contain(c => string.Equals(c.Type, "error", StringComparison.Ordinal));

        await hub.DisposeAsync();
    }

    [Fact]
    public async Task AgentHub_StreamChat_StartsWithStartChunk()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl($"{_client.BaseAddress}hubs/agent", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await hub.StartAsync();

        var chunks = new List<(string Type, string? Content)>();
        var stream = hub.StreamAsync<AgentStreamChunkDto>("StreamChat", "nonexistent-agent", "test");
        await foreach (var chunk in stream)
        {
            chunks.Add((chunk.Type, chunk.Content));
        }

        // First chunk should always be "start"
        chunks.Should().NotBeEmpty();
        chunks[0].Type.Should().Be("start");

        await hub.DisposeAsync();
    }

    // DTO matching the server's AgentStreamChunk record
    private sealed record AgentStreamChunkDto(string Type, string AgentId, string? Content);
}
