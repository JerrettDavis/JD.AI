using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Commands;
using JD.AI.Gateway.Config;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Commands;

public class GatewayCommandTests
{
    private readonly AgentPoolService _pool;
    private readonly ChannelRegistry _channels;
    private readonly AgentRouter _router;
    private readonly GatewayConfig _config;

    public GatewayCommandTests()
    {
        var providers = Substitute.For<IProviderRegistry>();
        var eventBus = Substitute.For<IEventBus>();
        _pool = new AgentPoolService(providers, eventBus, NullLogger<AgentPoolService>.Instance);
        _channels = new ChannelRegistry();
        _router = new AgentRouter(_pool, _channels, eventBus, NullLogger<AgentRouter>.Instance);
        _config = new GatewayConfig
        {
            Providers =
            [
                new ProviderConfig { Name = "Ollama", Enabled = true },
                new ProviderConfig { Name = "Claude", Enabled = false }
            ],
            Agents =
            [
                new AgentDefinition { Id = "default", Provider = "Ollama", Model = "llama3.2" }
            ]
        };
    }

    private static CommandContext MakeContext(string name, Dictionary<string, string>? args = null) => new()
    {
        CommandName = name,
        InvokerId = "user123",
        ChannelId = "ch456",
        ChannelType = "discord",
        Arguments = args ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    // --- StatusCommand ---

    [Fact]
    public async Task StatusCommand_ShowsChannelAndAgentInfo()
    {
        var cmd = new StatusCommand(_pool, _channels);

        var result = await cmd.ExecuteAsync(MakeContext("status"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("System Status");
        result.Content.Should().Contain("Channels:");
        result.Content.Should().Contain("Agents:");
    }

    [Fact]
    public async Task StatusCommand_ShowsConnectedChannels()
    {
        var mockChannel = Substitute.For<IChannel>();
        mockChannel.ChannelType.Returns("discord");
        mockChannel.DisplayName.Returns("Discord");
        mockChannel.IsConnected.Returns(true);
        _channels.Register(mockChannel);

        var cmd = new StatusCommand(_pool, _channels);
        var result = await cmd.ExecuteAsync(MakeContext("status"));

        result.Content.Should().Contain("Discord");
        result.Content.Should().Contain("Connected");
    }

    // --- UsageCommand ---

    [Fact]
    public async Task UsageCommand_ShowsUptimeAndAgentCount()
    {
        var cmd = new UsageCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("usage"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Usage Statistics");
        result.Content.Should().Contain("Uptime:");
        result.Content.Should().Contain("Active Agents:");
        result.Content.Should().Contain("Total Turns:");
    }

    // --- ModelsCommand ---

    [Fact]
    public async Task ModelsCommand_ShowsProvidersAndAgentDefs()
    {
        var cmd = new ModelsCommand(_pool, _config);

        var result = await cmd.ExecuteAsync(MakeContext("models"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("Ollama");
        result.Content.Should().Contain("default");
        result.Content.Should().Contain("llama3.2");
    }

    // --- AgentsCommand ---

    [Fact]
    public async Task AgentsCommand_WhenNoAgents_ShowsMessage()
    {
        var cmd = new AgentsCommand(_pool, _router);

        var result = await cmd.ExecuteAsync(MakeContext("agents"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agents are running");
    }

    // --- ClearCommand ---

    [Fact]
    public async Task ClearCommand_WhenNoAgents_ShowsInfo()
    {
        var cmd = new ClearCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("clear"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("No agents are running");
    }

    // --- SwitchCommand ---

    [Fact]
    public async Task SwitchCommand_WithoutModel_ReturnsError()
    {
        var cmd = new SwitchCommand(_pool);

        var result = await cmd.ExecuteAsync(MakeContext("switch"));

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("specify a model");
    }

    // --- HelpCommand integration ---

    [Fact]
    public async Task HelpCommand_ListsAllCommands()
    {
        var registry = new CommandRegistry();
        var helpCmd = new HelpCommand(registry);
        registry.Register(helpCmd);
        registry.Register(new UsageCommand(_pool));
        registry.Register(new StatusCommand(_pool, _channels));

        var result = await helpCmd.ExecuteAsync(MakeContext("help"));

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("jdai-help");
        result.Content.Should().Contain("jdai-usage");
        result.Content.Should().Contain("jdai-status");
    }
}
