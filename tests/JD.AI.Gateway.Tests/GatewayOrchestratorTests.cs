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
        _providerDetector.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(_ => CreateKernel());

        var fakeProviders = Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                new ProviderInfo(
                    "fake",
                    true,
                    "ready",
                    [
                        new ProviderModelInfo("model-a", "Model A", "fake"),
                    ])
            ]);
        _providerRegistry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(fakeProviders);
        _providerRegistry.DetectProvidersAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(fakeProviders);
        _providerRegistry.GetDetector(Arg.Any<string>()).Returns((IProviderDetector?)null);
        _providerRegistry.GetDetector("fake").Returns(_providerDetector);

        _pool = new AgentPoolService(
            _providerRegistry,
            _channels,
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

    [Fact]
    public async Task StartAsync_WithMultipleSameTypeChannels_UsesChannelNameForDedicatedRouting()
    {
        _config.Channels =
        [
            new ChannelConfig
            {
                Type = "web",
                Name = "support",
                Enabled = true,
                AutoConnect = false,
            },
            new ChannelConfig
            {
                Type = "web",
                Name = "sales",
                Enabled = true,
                AutoConnect = false,
            },
        ];
        _config.Agents =
        [
            new AgentDefinition
            {
                Id = "assistant-a",
                Provider = "fake",
                Model = "model-a",
                AutoSpawn = true,
            },
            new AgentDefinition
            {
                Id = "assistant-b",
                Provider = "fake",
                Model = "model-a",
                AutoSpawn = true,
            }
        ];
        _config.Routing = new RoutingConfig
        {
            Rules =
            [
                new RoutingRule { ChannelType = "web", ChannelName = "support", AgentId = "assistant-a" },
                new RoutingRule { ChannelType = "web", ChannelName = "sales", AgentId = "assistant-b" }
            ]
        };

        var orchestrator = CreateOrchestrator();
        await orchestrator.StartAsync(CancellationToken.None);

        var mappings = _router.GetMappings();
        mappings.Should().ContainKey("web:support");
        mappings.Should().ContainKey("web:sales");
        mappings["web:support"].Should().NotBe(mappings["web:sales"]);
    }

    [Theory]
    [InlineData("!model list")]
    [InlineData("!model current")]
    [InlineData("!model set gpt-4o")]
    [InlineData("<@123456789> !model list")]
    public async Task DirectDiscord_FastPathModelCommands_AreHandledWithoutAgentRouting(string message)
    {
        var discord = new TestChannel("discord");
        _channels.Register(discord);

        _config.Channels =
        [
            new ChannelConfig { Type = "discord", Name = "Discord", Enabled = true, AutoConnect = false }
        ];

        var commands = new CommandRegistry();
        commands.Register(new ResponseCommand("models", "models-output"));
        commands.Register(new ResponseCommand("status", "status-output"));
        commands.Register(new ResponseCommand("switch", "switch-output",
            [new CommandParameter { Name = "model", Description = "model", IsRequired = true }]));

        var orchestrator = CreateOrchestrator(commands);
        await orchestrator.StartAsync(CancellationToken.None);

        await discord.EmitMessageAsync(message);

        discord.SendCalls.Should().Be(1);
        discord.LastSentContent.Should().BeOneOf("models-output", "status-output", "switch-output");
        _pool.ListAgents().Should().BeEmpty("fast-path command handling should not require LLM agent routing");
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

    private sealed class ResponseCommand : IChannelCommand
    {
        private readonly string _response;

        public ResponseCommand(string name, string response, IReadOnlyList<CommandParameter>? parameters = null)
        {
            Name = name;
            _response = response;
            Parameters = parameters ?? [];
        }

        public string Name { get; }
        public string Description => Name;
        public IReadOnlyList<CommandParameter> Parameters { get; }

        public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default) =>
            Task.FromResult(new CommandResult { Success = true, Content = _response });
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
        public string? LastSentContent { get; private set; }
        public event Func<ChannelMessage, Task>? MessageReceived;

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
            LastSentContent = content;
            return Task.CompletedTask;
        }

        public async Task EmitMessageAsync(string content)
        {
            if (MessageReceived is null)
                return;

            await MessageReceived.Invoke(new ChannelMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                ChannelId = "test-channel",
                SenderId = "user-1",
                SenderDisplayName = "User",
                Content = content,
                Timestamp = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, string>(),
                Attachments = []
            });
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
