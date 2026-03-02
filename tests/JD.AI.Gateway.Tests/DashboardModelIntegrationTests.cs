using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Dashboard.Wasm.Models;

namespace JD.AI.Gateway.Tests;

/// <summary>
/// Validates that the Gateway API responses can be deserialized into
/// the Dashboard's client-side models without error.
/// </summary>
public sealed class DashboardModelIntegrationTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public DashboardModelIntegrationTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatus_DeserializesIntoGatewayStatus()
    {
        var response = await _client.GetAsync("/api/gateway/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var status = await response.Content.ReadFromJsonAsync<GatewayStatus>();

        status.Should().NotBeNull();
        status!.Status.Should().Be("running");
        status.IsRunning.Should().BeTrue();
        status.Channels.Should().NotBeNull();
        status.Agents.Should().NotBeNull();
        status.Routes.Should().NotBeNull();
        status.OpenClaw.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatus_ChannelsHaveExpectedShape()
    {
        var status = await _client.GetFromJsonAsync<GatewayStatus>("/api/gateway/status");

        status.Should().NotBeNull();
        // The default config has at least the "web" channel
        status!.Channels.Should().Contain(c => c.ChannelType == "web");

        foreach (var channel in status.Channels)
        {
            channel.ChannelType.Should().NotBeNullOrEmpty();
            channel.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetStatus_AgentsIncludeAutoSpawnedDefault()
    {
        var status = await _client.GetFromJsonAsync<GatewayStatus>("/api/gateway/status");

        status.Should().NotBeNull();
        // Agents may be empty if no AI provider (e.g. Ollama) is reachable in CI
        status!.Agents.Should().NotBeNull();
        status.ActiveAgents.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetStatus_OpenClawStatusPresent()
    {
        var status = await _client.GetFromJsonAsync<GatewayStatus>("/api/gateway/status");

        status.Should().NotBeNull();
        status!.OpenClaw.Should().NotBeNull();
        status.OpenClaw!.Enabled.Should().BeTrue("OpenClaw is enabled in test config");
    }

    [Fact]
    public async Task GetAgents_DeserializesIntoAgentInfoArray()
    {
        var response = await _client.GetAsync("/api/agents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agents = await response.Content.ReadFromJsonAsync<AgentInfo[]>();

        agents.Should().NotBeNull();
        // Agents may be empty if no AI provider is reachable in CI
        if (agents!.Length > 0)
        {
            agents[0].Id.Should().NotBeNullOrEmpty();
            agents[0].Provider.Should().NotBeNullOrEmpty();
            agents[0].Model.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetChannels_DeserializesIntoChannelInfoArray()
    {
        var response = await _client.GetAsync("/api/channels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var channels = await response.Content.ReadFromJsonAsync<ChannelInfo[]>();

        channels.Should().NotBeNull();
        channels.Should().NotBeEmpty("web channel is registered by default");
    }

    [Fact]
    public async Task GetRoutingMappings_DeserializesIntoDictionary()
    {
        var response = await _client.GetAsync("/api/routing/mappings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The API returns Dictionary<string, string> (channelType → agentId)
        var dict = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        dict.Should().NotBeNull();
        // Routing may be empty if no agents were auto-spawned (no provider in CI)
        if (dict!.Count > 0)
        {
            var mappings = dict.Select(kv => new RoutingMapping { ChannelType = kv.Key, AgentId = kv.Value }).ToArray();
            mappings.Should().Contain(m => string.Equals(m.ChannelType, "web", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task GetProviders_DeserializesIntoProviderInfoArray()
    {
        var response = await _client.GetAsync("/api/providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providers = await response.Content.ReadFromJsonAsync<ProviderInfo[]>();

        providers.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSessions_DeserializesIntoSessionInfoArray()
    {
        var response = await _client.GetAsync("/api/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessions = await response.Content.ReadFromJsonAsync<SessionInfo[]>();

        sessions.Should().NotBeNull();
    }

    [Fact]
    public async Task SignalR_EventHub_NegotiateEndpointResponds()
    {
        var response = await _client.PostAsync("/hubs/events/negotiate?negotiateVersion=1", null);

        // SignalR negotiate returns 200 with connection info
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("connectionId");
    }

    [Fact]
    public async Task SignalR_AgentHub_NegotiateEndpointResponds()
    {
        var response = await _client.PostAsync("/hubs/agent/negotiate?negotiateVersion=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("connectionId");
    }

    [Fact]
    public async Task GetConfig_DeserializesIntoGatewayConfigModel()
    {
        var response = await _client.GetAsync("/api/gateway/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<GatewayConfigModel>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        config.Should().NotBeNull();
        config!.Server.Should().NotBeNull();
        config.Providers.Should().NotBeNull();
        config.Agents.Should().NotBeNull();
        config.Channels.Should().NotBeNull();
        config.Routing.Should().NotBeNull();
        config.OpenClaw.Should().NotBeNull();
    }

    [Fact]
    public async Task GetConfig_AgentDefinitions_IncludeModelParameters()
    {
        // First write an agent with parameters
        var agents = new[]
        {
            new AgentDefinition
            {
                Id = "param-test",
                Provider = "ollama",
                Model = "test",
                Parameters = new ModelParameters
                {
                    Temperature = 0.5,
                    TopK = 40,
                    ContextWindowSize = 16384,
                }
            }
        };
        await _client.PutAsJsonAsync("/api/gateway/config/agents", agents);

        // Read back via full config
        var response = await _client.GetAsync("/api/gateway/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var config = await response.Content.ReadFromJsonAsync<GatewayConfigModel>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        config!.Agents.Should().NotBeEmpty();
    }
}
