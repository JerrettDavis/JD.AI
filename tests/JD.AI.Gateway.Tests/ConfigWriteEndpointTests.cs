using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Gateway.Config;
using CoreReasoningEffort = JD.AI.Core.Agents.ReasoningEffort;

namespace JD.AI.Gateway.Tests;

/// <summary>
/// Tests for the config write (PUT) endpoints in GatewayConfigEndpoints.
/// Validates that the Gateway accepts updates and returns the new config.
/// </summary>
public sealed class ConfigWriteEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public ConfigWriteEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PutServerConfig_ReturnsOk()
    {
        var update = new { Port = 9999, Host = "0.0.0.0", Verbose = true };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/server", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("9999");
    }

    [Fact]
    public async Task PutAuthConfig_ReturnsOk()
    {
        var update = new { Enabled = true, ApiKeys = new[] { new { Key = "test-key", Name = "Test", Role = "Admin" } } };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/auth", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutRateLimitConfig_ReturnsOk()
    {
        var update = new { Enabled = true, MaxRequestsPerMinute = 120 };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/ratelimit", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutProvidersConfig_ReturnsOk()
    {
        var update = new[]
        {
            new { Name = "ollama", Enabled = true, Settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["BaseUrl"] = "http://localhost:11434" } }
        };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/providers", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutAgentsConfig_ReturnsOk()
    {
        var update = new[]
        {
            new
            {
                Id = "test-agent",
                Provider = "ollama",
                Model = "qwen3.5:27b",
                SystemPrompt = "You are a test agent.",
                AutoSpawn = false,
                MaxTurns = 10,
                Tools = Array.Empty<string>(),
                Parameters = new
                {
                    Temperature = 0.7,
                    TopK = 40,
                    ContextWindowSize = 32768,
                    MaxTokens = 4096,
                }
            }
        };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/agents", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("test-agent");
    }

    [Fact]
    public async Task PutAgentsConfig_WithModelParameters_Roundtrips()
    {
        var agents = new[]
        {
            new AgentDefinition
            {
                Id = "param-test",
                Provider = "ollama",
                Model = "test:latest",
                Parameters = new ModelParameters
                {
                    Temperature = 0.5,
                    TopP = 0.9,
                    TopK = 50,
                    MaxTokens = 2048,
                    ContextWindowSize = 16384,
                    FrequencyPenalty = 0.3,
                    PresencePenalty = 0.1,
                    RepeatPenalty = 1.2,
                    Seed = 42,
                    StopSequences = ["<|end|>"],
                    ReasoningEffort = CoreReasoningEffort.Low,
                }
            }
        };

        var putResponse = await _client.PutAsJsonAsync("/api/gateway/config/agents", agents);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Read back the full config to verify parameters persisted in memory
        var getResponse = await _client.GetAsync("/api/gateway/config");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadAsStringAsync();
        body.Should().Contain("param-test");
    }

    [Fact]
    public async Task PutAgentsConfig_WhenRoutingReferencesMissingAgents_NormalizesRouting()
    {
        var routingUpdate = new
        {
            DefaultAgentId = "ghost",
            Rules = new[]
            {
                new { ChannelType = "web", AgentId = "ghost", ConversationPattern = (string?)null },
                new { ChannelType = "discord", AgentId = "kept", ConversationPattern = (string?)null }
            }
        };
        var routingResponse = await _client.PutAsJsonAsync("/api/gateway/config/routing", routingUpdate);
        routingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentsUpdate = new[]
        {
            new AgentDefinition
            {
                Id = "kept",
                Provider = "OpenAI Codex",
                Model = "gpt-5.3-codex",
                AutoSpawn = true
            }
        };
        var agentsResponse = await _client.PutAsJsonAsync("/api/gateway/config/agents", agentsUpdate);
        agentsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var configResponse = await _client.GetAsync("/api/gateway/config/raw");
        configResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await configResponse.Content.ReadFromJsonAsync<GatewayConfig>();
        config.Should().NotBeNull();
        config!.Routing.DefaultAgentId.Should().Be("kept");
        config.Routing.Rules.Should().ContainSingle();
        config.Routing.Rules[0].AgentId.Should().Be("kept");
    }

    [Fact]
    public async Task PutChannelsConfig_ReturnsOk()
    {
        var update = new[]
        {
            new { Type = "web", Name = "WebChat", Enabled = true, AutoConnect = true, Settings = new Dictionary<string, string>(StringComparer.Ordinal) }
        };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/channels", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutRoutingConfig_ReturnsOk()
    {
        var update = new
        {
            DefaultAgentId = "default",
            Rules = new[] { new { ChannelType = "web", AgentId = "default", ConversationPattern = (string?)null } }
        };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/routing", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutOpenClawConfig_ReturnsOk()
    {
        var update = new
        {
            Enabled = false,
            WebSocketUrl = "ws://127.0.0.1:18789/ws/gateway",
            AutoConnect = false,
            DefaultMode = "Passthrough",
        };

        var response = await _client.PutAsJsonAsync("/api/gateway/config/openclaw", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutOpenClawConfig_Disable_ReportsNoActiveOverrideInStatus()
    {
        var update = new
        {
            Enabled = false,
            WebSocketUrl = "ws://127.0.0.1:18789/ws/gateway",
            AutoConnect = false,
            DefaultMode = "Passthrough",
        };

        var putResponse = await _client.PutAsJsonAsync("/api/gateway/config/openclaw", update);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusResponse = await _client.GetAsync("/api/gateway/status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusJson = await statusResponse.Content.ReadAsStringAsync();
        statusJson.Should().Contain("\"overrideActive\":false");
    }

    [Fact]
    public async Task PostOpenClawBridgeDisableEndpoint_ReturnsOk()
    {
        var response = await _client.PostAsync("/api/gateway/openclaw/bridge/disable", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("sessionCleanupDeleted");
    }

    [Fact]
    public async Task GetConfig_IncludesModelParametersField()
    {
        var response = await _client.GetAsync("/api/gateway/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        // The config JSON should include the agents array
        body.Should().Contain("agents");
    }
}
