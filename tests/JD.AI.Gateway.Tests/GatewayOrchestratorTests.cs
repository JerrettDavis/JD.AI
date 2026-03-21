using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Gateway.Tests;

public sealed class GatewayOrchestratorTests
{
    private readonly GatewayConfig _config = new();
    private readonly ChannelRegistry _channels = new();
    private readonly IEventBus _events = new InProcessEventBus();
    private readonly IProviderRegistry _providerRegistry;
    private readonly IProviderDetector _providerDetector;
    private readonly AgentPoolService _pool;
    private readonly AgentRouter _router;
    private readonly ChannelFactory _factory;

    public GatewayOrchestratorTests()
    {
        _config.OpenClaw.Enabled = false;

        _providerRegistry = Substitute.For<IProviderRegistry>();
        _providerDetector = Substitute.For<IProviderDetector>();
        _providerDetector.ProviderName.Returns("fake");
        _providerDetector.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(CreateKernel());

        _providerRegistry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
                [
                    new ProviderInfo(
                        "fake",
                        true,
                        "ready",
                        [
                            new ProviderModelInfo("model-a", "Model A", "fake"),
                        ])
                ]));
        _providerRegistry.GetDetector(Arg.Any<string>()).Returns((IProviderDetector?)null);
        _providerRegistry.GetDetector("fake").Returns(_providerDetector);

        _pool = new AgentPoolService(
            _providerRegistry,
            _events,
            NullLogger<AgentPoolService>.Instance);
        _router = new AgentRouter(
            _pool,
            _channels,
            _events,
            NullLogger<AgentRouter>.Instance);
        _factory = new ChannelFactory(
            Substitute.For<IServiceProvider>(),
            NullLogger<ChannelFactory>.Instance);
    }

    [Fact]
    public async Task StartAsync_AutoSpawnsAgent_RoutesChannels_AndRegistersCommands()
    {
        var manualChannel = new TestChannel("manual");
        var commandAwareChannel = new CommandAwareTestChannel("commands");
        _channels.Register(manualChannel);
        _channels.Register(commandAwareChannel);

        _config.Channels =
        [
            new ChannelConfig
            {
                Type = "web",
                Name = "Web",
                Enabled = true,
                AutoConnect = false,
            },
            new ChannelConfig
            {
                Type = "manual",
                Name = "Manual",
                Enabled = true,
                AutoConnect = true,
            },
        ];
        _config.Agents =
        [
            new AgentDefinition
            {
                Id = "assistant",
                Provider = "fake",
                Model = "model-a",
                AutoSpawn = true,
                SystemPrompt = "You are a test assistant.",
            }
        ];
        _config.Routing = new RoutingConfig
        {
            DefaultAgentId = "assistant",
            Rules =
            [
                new RoutingRule { ChannelType = "web", AgentId = "assistant" }
            ]
        };

        var commands = new CommandRegistry();
        commands.Register(new TestCommand());

        var orchestrator = CreateOrchestrator(commands);

        await orchestrator.StartAsync(CancellationToken.None);

        var agentId = _pool.ListAgents().Should().ContainSingle().Which.Id;
        var mappings = _router.GetMappings();
        mappings.Should().ContainKey("web");
        mappings.Should().ContainKey("manual");
        mappings.Should().ContainKey("commands");
        mappings["web"].Should().Be(agentId);
        mappings["manual"].Should().Be(agentId);
        mappings["commands"].Should().Be(agentId);

        manualChannel.ConnectCalls.Should().Be(1);
        manualChannel.IsConnected.Should().BeTrue();
        commandAwareChannel.RegisterCommandsCalls.Should().Be(1);
        commandAwareChannel.LastRegistryCommandCount.Should().Be(1);

        await orchestrator.StopAsync(CancellationToken.None);

        manualChannel.DisconnectCalls.Should().Be(1);
        manualChannel.IsConnected.Should().BeFalse();
        _pool.ListAgents().Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WithEmptyCommandRegistry_SkipsCommandAwareChannels()
    {
        var commandAwareChannel = new CommandAwareTestChannel("commands");
        _channels.Register(commandAwareChannel);

        _config.Agents =
        [
            new AgentDefinition
            {
                Id = "assistant",
                Provider = "fake",
                Model = "model-a",
                AutoSpawn = true,
            }
        ];
        _config.Routing.DefaultAgentId = "assistant";

        var orchestrator = CreateOrchestrator(new CommandRegistry());

        await orchestrator.StartAsync(CancellationToken.None);

        commandAwareChannel.RegisterCommandsCalls.Should().Be(0);
        _pool.ListAgents().Should().ContainSingle();
    }

    [Fact]
    public async Task StopAsync_DisconnectsConnectedChannels_AndClearsSpawnedAgents()
    {
        var channel = new TestChannel("manual");
        _channels.Register(channel);

        _config.Channels =
        [
            new ChannelConfig
            {
                Type = "manual",
                Name = "Manual",
                Enabled = true,
                AutoConnect = true,
            },
        ];
        _config.Agents =
        [
            new AgentDefinition
            {
                Id = "assistant",
                Provider = "fake",
                Model = "model-a",
                AutoSpawn = true,
            }
        ];
        _config.Routing.DefaultAgentId = "assistant";

        var orchestrator = CreateOrchestrator();

        await orchestrator.StartAsync(CancellationToken.None);
        await orchestrator.StopAsync(CancellationToken.None);

        channel.ConnectCalls.Should().Be(1);
        channel.DisconnectCalls.Should().Be(1);
        _pool.ListAgents().Should().BeEmpty();
    }

    private GatewayOrchestrator CreateOrchestrator(ICommandRegistry? commandRegistry = null) => new(
        _config,
        _factory,
        _channels,
        _pool,
        _router,
        _events,
        NullLogger<GatewayOrchestrator>.Instance,
        commandRegistry: commandRegistry);

    private static Kernel CreateKernel() => Kernel.CreateBuilder().Build();

    private sealed class TestCommand : IChannelCommand
    {
        public string Name => "ping";
        public string Description => "Ping command for test registration.";
        public IReadOnlyList<CommandParameter> Parameters => [];

        public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default) =>
            Task.FromResult(new CommandResult { Success = true, Content = "pong" });
    }

    private class TestChannel : IChannel
    {
        public TestChannel(string channelType)
        {
            ChannelType = channelType;
            DisplayName = channelType;
        }

        public string ChannelType { get; }
        public string DisplayName { get; }
        public bool IsConnected { get; protected set; }
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public int SendCalls { get; private set; }
#pragma warning disable CS0067 // Test double event is intentionally unused.
        public event Func<ChannelMessage, Task>? MessageReceived;
#pragma warning restore CS0067

        public virtual Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public virtual Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCalls++;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public virtual Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
        {
            SendCalls++;
            return Task.CompletedTask;
        }

        public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CommandAwareTestChannel : TestChannel, ICommandAwareChannel
    {
        public CommandAwareTestChannel(string channelType) : base(channelType)
        {
        }

        public int RegisterCommandsCalls { get; private set; }
        public int LastRegistryCommandCount { get; private set; }

        public Task RegisterCommandsAsync(ICommandRegistry registry, CancellationToken ct = default)
        {
            RegisterCommandsCalls++;
            LastRegistryCommandCount = registry.Commands.Count;
            return Task.CompletedTask;
        }
    }
}
