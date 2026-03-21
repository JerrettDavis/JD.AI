using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using JD.AI.Channels.OpenClaw;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JD.AI.Gateway.Tests;

public sealed class GatewayConfigEndpointTests : IClassFixture<GatewayTestFactory>
{
    private readonly HttpClient _client;

    public GatewayConfigEndpointTests(GatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConfig_ReturnsOkWithStructure()
    {
        var response = await _client.GetAsync("/api/gateway/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("server");
        body.Should().Contain("channels");
        body.Should().Contain("agents");
        body.Should().Contain("routing");
        body.Should().Contain("openClaw");
    }

    [Fact]
    public async Task GetConfig_RedactsSecrets()
    {
        var response = await _client.GetAsync("/api/gateway/config");
        var body = await response.Content.ReadAsStringAsync();

        // Env-ref tokens should remain visible as references, but not expose actual values
        // Settings that start with "env:" keep their reference form
        body.Should().NotContain("actual-secret-value");
    }

    [Fact]
    public async Task GetStatus_ReturnsRunningStatus()
    {
        var response = await _client.GetAsync("/api/gateway/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("running");
    }

    [Fact]
    public async Task GetStatus_IncludesChannelsAgentsRoutes()
    {
        var response = await _client.GetAsync("/api/gateway/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("channels", out _).Should().BeTrue();
        json.TryGetProperty("agents", out _).Should().BeTrue();
        json.TryGetProperty("routes", out _).Should().BeTrue();
        json.TryGetProperty("openClaw", out var openClaw).Should().BeTrue();
        openClaw.TryGetProperty("connected", out _).Should().BeTrue();
        openClaw.TryGetProperty("overrideActive", out _).Should().BeTrue();
        openClaw.TryGetProperty("overrideChannels", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetConfig_RedactsPlainSecrets_AndKeepsEnvReferences()
    {
        var update = new[]
        {
            new ChannelConfig
            {
                Type = "discord",
                Name = "Discord",
                Enabled = true,
                AutoConnect = false,
                Settings = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["BotToken"] = "super-secret",
                    ["AppToken"] = "env:DISCORD_APP_TOKEN",
                }
            }
        };

        var putResponse = await _client.PutAsJsonAsync("/api/gateway/config/channels", update);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync("/api/gateway/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var channels = json.RootElement.GetProperty("channels");
        var discord = GetChannel(channels, "discord");
        var settings = discord.GetProperty("settings");

        settings.GetProperty("BotToken").GetString().Should().Be("***");
        settings.GetProperty("AppToken").GetString().Should().Be("env:DISCORD_APP_TOKEN");
        body.Should().NotContain("super-secret");
    }

    [Fact]
    public async Task GetStatus_WithOpenClawOverrides_ReturnsSortedOverrideChannels()
    {
        var update = new OpenClawGatewayConfig
        {
            Enabled = true,
            AutoConnect = false,
            DefaultMode = "Passthrough",
            Channels = new Dictionary<string, OpenClawChannelConfig>(StringComparer.Ordinal)
            {
                ["slack"] = new OpenClawChannelConfig { Mode = "Sidecar" },
                ["discord"] = new OpenClawChannelConfig { Mode = "Passthrough" },
                ["signal"] = new OpenClawChannelConfig { Mode = "Intercept" },
            }
        };

        var putResponse = await _client.PutAsJsonAsync("/api/gateway/config/openclaw", update);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync("/api/gateway/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var openClaw = json.RootElement.GetProperty("openClaw");

        openClaw.GetProperty("overrideActive").GetBoolean().Should().BeTrue();

        var overrideChannels = openClaw.GetProperty("overrideChannels");
        overrideChannels.GetArrayLength().Should().Be(2);
        overrideChannels[0].GetString().Should().Be("signal");
        overrideChannels[1].GetString().Should().Be("slack");
    }

    [Fact]
    public void BuildManagedSessionPrefixes_TrimsAndDeduplicatesRegistrationIds()
    {
        var config = new OpenClawGatewayConfig
        {
            RegisterAgents =
            [
                new OpenClawAgentRegistration { Id = " alpha " },
                new OpenClawAgentRegistration { Id = "alpha" },
                new OpenClawAgentRegistration { Id = "beta" },
                new OpenClawAgentRegistration { Id = "   " },
            ]
        };

        var prefixes = InvokePrivateStatic<string[]>("BuildManagedSessionPrefixes", config);

        prefixes.Should().Contain($"agent:{OpenClawAgentRegistrar.AgentIdPrefix}");
        prefixes.Should().Contain("agent:alpha:");
        prefixes.Should().Contain("agent:beta:");
        prefixes.Should().HaveCount(3);
    }

    [Fact]
    public void BuildManagedSessionContains_CollectsRegistrationsBindingsAndChannels()
    {
        var config = new OpenClawGatewayConfig
        {
            RegisterAgents =
            [
                new OpenClawAgentRegistration
                {
                    Id = "alpha",
                    Bindings =
                    [
                        new OpenClawBindingConfig { Channel = "discord" },
                        new OpenClawBindingConfig { Channel = " " },
                    ]
                }
            ],
            Channels = new Dictionary<string, OpenClawChannelConfig>(StringComparer.Ordinal)
            {
                ["signal"] = new OpenClawChannelConfig(),
                [" "] = new OpenClawChannelConfig(),
            }
        };

        var fragments = InvokePrivateStatic<string[]>("BuildManagedSessionContains", config);

        fragments.Should().Contain("g-agent-");
        fragments.Should().Contain("alpha");
        fragments.Should().Contain("discord:g-agent-");
        fragments.Should().Contain("signal:g-agent-");
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(GatewayConfigEndpoints).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull($"expected {methodName} to exist");
        return (T)method!.Invoke(null, args)!;
    }

    private static JsonElement GetChannel(JsonElement channels, string channelType)
    {
        foreach (var channel in channels.EnumerateArray())
        {
            if (string.Equals(channel.GetProperty("type").GetString(), channelType, StringComparison.OrdinalIgnoreCase))
                return channel;
        }

        throw new InvalidOperationException($"Channel '{channelType}' not found.");
    }
}

public sealed class OpenClawDisabledGatewayTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Gateway:OpenClaw:Enabled", "false");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        catch (AggregateException)
        {
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (AggregateException)
        {
        }

        GC.SuppressFinalize(this);
    }
}

public sealed class OpenClawDisabledGatewayEndpointTests : IClassFixture<OpenClawDisabledGatewayTestFactory>
{
    private readonly HttpClient _client;

    public OpenClawDisabledGatewayEndpointTests(OpenClawDisabledGatewayTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOpenClawAgents_WhenDisabled_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/gateway/openclaw/agents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("agents").ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetProperty("agents").GetArrayLength().Should().Be(0);
        json.RootElement.GetProperty("message").GetString().Should().Be("OpenClaw integration not enabled");
    }

    [Fact]
    public async Task GetOpenClawStatus_WhenDisabled_ReturnsDisabledPayload()
    {
        var response = await _client.GetAsync("/api/gateway/openclaw/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("enabled").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("message").GetString().Should().Be("OpenClaw integration not enabled");
    }

    [Fact]
    public async Task PostOpenClawBridgeDisable_WhenDisabled_ReturnsNoopResult()
    {
        var response = await _client.PostAsync("/api/gateway/openclaw/bridge/disable", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("bridgeDisabled").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("sessionCleanupDeleted").GetInt32().Should().Be(0);
        json.RootElement.GetProperty("message").GetString().Should().Be("OpenClaw integration not enabled");
    }
}
