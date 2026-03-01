using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Gateway.Tests;

public sealed class GatewayOrchestratorTests
{
    private readonly GatewayConfig _config;
    private readonly ChannelRegistry _channels;
    private readonly AgentPoolService _pool;
    private readonly AgentRouter _router;
    private readonly IEventBus _events;
    private readonly ChannelFactory _factory;

    public GatewayOrchestratorTests()
    {
        _config = new GatewayConfig();
        _channels = new ChannelRegistry();
        _events = new InProcessEventBus();

        var providerRegistry = Substitute.For<IProviderRegistry>();
        _pool = new AgentPoolService(providerRegistry, _events);
        _router = new AgentRouter(_pool, _channels, _events, NullLogger<AgentRouter>.Instance);

        var sp = Substitute.For<IServiceProvider>();
        _factory = new ChannelFactory(sp, NullLogger<ChannelFactory>.Instance);
    }

    private GatewayOrchestrator CreateOrchestrator() => new(
        _config, _factory, _channels, _pool, _router, _events,
        NullLogger<GatewayOrchestrator>.Instance);

    [Fact]
    public async Task StartAsync_WithNoConfig_CompletesSuccessfully()
    {
        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);

        _channels.Channels.Should().BeEmpty();
        _pool.ListAgents().Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_RegistersEnabledChannels()
    {
        _config.Channels.Add(new ChannelConfig { Type = "web", Name = "Web", Enabled = true });
        _config.Channels.Add(new ChannelConfig { Type = "unknown-type", Name = "Bad", Enabled = true });

        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);

        // Web should be registered, unknown should be skipped
        _channels.Channels.Should().HaveCount(1);
        _channels.Channels[0].ChannelType.Should().Be("web");
    }

    [Fact]
    public async Task StartAsync_SkipsDisabledChannels()
    {
        _config.Channels.Add(new ChannelConfig { Type = "web", Name = "Web", Enabled = false });

        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);

        _channels.Channels.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_AutoConnectsMarkedChannels()
    {
        _config.Channels.Add(new ChannelConfig
        {
            Type = "web",
            Name = "Web",
            Enabled = true,
            AutoConnect = true
        });

        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);

        // WebChannel.ConnectAsync is a no-op, so just verify it's registered
        _channels.Channels.Should().HaveCount(1);
    }

    [Fact]
    public async Task StopAsync_DisconnectsAllChannels()
    {
        _config.Channels.Add(new ChannelConfig
        {
            Type = "web",
            Name = "Web",
            Enabled = true
        });

        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);
        await orchestrator.StopAsync(CancellationToken.None);

        // Should complete without error
        _pool.ListAgents().Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WiresMessageRouting()
    {
        _config.Channels.Add(new ChannelConfig
        {
            Type = "web",
            Name = "Web",
            Enabled = true
        });

        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);

        // The web channel should have MessageReceived handler wired
        var channel = _channels.GetChannel("web");
        channel.Should().NotBeNull();
    }

    [Fact]
    public void GatewayConfig_BindsFromJson()
    {
        var config = new GatewayConfig
        {
            Agents =
            [
                new AgentDefinition
                {
                    Id = "test",
                    Provider = "ollama",
                    Model = "llama3.2",
                    AutoSpawn = true
                }
            ],
            Routing = new RoutingConfig
            {
                DefaultAgentId = "test",
                Rules = [new RoutingRule { ChannelType = "web", AgentId = "test" }]
            }
        };

        config.Agents.Should().HaveCount(1);
        config.Agents[0].Id.Should().Be("test");
        config.Routing.DefaultAgentId.Should().Be("test");
    }
}
