using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Commands;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using NSubstitute;
using System.Reflection;

namespace JD.AI.Gateway.Tests.Commands;

public sealed class ConfigCommandTests
{
    private static readonly ProviderModelInfo OllamaModel = new("llama3.2:latest", "llama3.2:latest", "Ollama");
    private static readonly ProviderModelInfo OpenAiModel = new("gpt-5.3-codex", "GPT-5.3 Codex", "OpenAI");

    private static CommandContext MakeContext() => new()
    {
        CommandName = "config",
        InvokerId = "user123",
        ChannelId = "ch456",
        ChannelType = "discord",
    };

    [Fact]
    public void Metadata_IsExpected()
    {
        var (command, _, _, _) = CreateCommand([]);

        command.Name.Should().Be("config");
        command.Description.Should().Be("Prints provider, route, agent, and channel configuration.");
        command.Parameters.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyState_ShowsProvidersAndEmptySections()
    {
        var (command, _, _, _) = CreateCommand(
        [
            new ProviderInfo("Ollama", true, null, [OllamaModel]),
            new ProviderInfo("Claude", false, "offline", [])
        ]);

        var result = await command.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("**JD.AI Configuration**");
        result.Content.Should().Contain("🟢 Ollama — 1 model(s)");
        result.Content.Should().Contain("🔴 Claude — 0 model(s)");
        result.Content.Should().Contain("**Agents:**");
        result.Content.Should().Contain("(none running)");
        result.Content.Should().Contain("**Routes:**");
        result.Content.Should().Contain("(no routes configured)");
        result.Content.Should().Contain("**Channels:**");
        result.Content.Should().Contain("(none registered)");
    }

    [Fact]
    public async Task ExecuteAsync_WithRunningAgentRouteAndChannels_ShowsDetailedConfiguration()
    {
        var (command, pool, channels, router) = CreateCommand(
        [
            new ProviderInfo("Ollama", true, null, [OllamaModel]),
            new ProviderInfo("OpenAI", true, null, [OpenAiModel])
        ]);

        var agentId = await pool.SpawnAgentAsync("Ollama", "llama3.2:latest", null, CancellationToken.None);
        router.MapChannel("discord", agentId);
        channels.Register(new TestChannel("discord", "Discord", isConnected: true));
        channels.Register(new TestChannel("slack", "Slack", isConnected: false));

        var result = await command.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain($"🤖 `{agentId[..8]}` — Ollama/llama3.2:latest — 0 turns");
        result.Content.Should().Contain("📡 discord → Ollama/llama3.2:latest");
        result.Content.Should().Contain("🟢 Discord (discord)");
        result.Content.Should().Contain("🔴 Slack (slack)");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRouteTargetsMissingAgent_ShowsTruncatedAgentId()
    {
        var (command, _, _, router) = CreateCommand(
        [
            new ProviderInfo("Ollama", true, null, [OllamaModel])
        ]);

        router.MapChannel("discord", "1234567890ab");

        var result = await command.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("📡 discord → 12345678");
    }

    [Theory]
    [InlineData(45, "45m")]
    [InlineData(120, "2h")]
    public void FormatAge_FormatsMinutesAndHours(double minutes, string expected)
    {
        var method = typeof(ConfigCommand).GetMethod("FormatAge", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var formatted = method!.Invoke(null, [TimeSpan.FromMinutes(minutes)]);

        formatted.Should().Be(expected);
    }

    private static (ConfigCommand Command, AgentPoolService Pool, ChannelRegistry Channels, AgentRouter Router) CreateCommand(
        IReadOnlyList<ProviderInfo> providers)
    {
        var registry = Substitute.For<IProviderRegistry>();
        var detector = Substitute.For<IProviderDetector>();
        detector.BuildKernel(Arg.Any<ProviderModelInfo>()).Returns(_ => Kernel.CreateBuilder().Build());

        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providers));
        registry.GetDetector(Arg.Any<string>())
            .Returns(detector);

        var eventBus = Substitute.For<IEventBus>();
        var channels = new ChannelRegistry();
        var pool = new AgentPoolService(registry, channels, eventBus, NullLogger<AgentPoolService>.Instance);
        var router = new AgentRouter(pool, channels, eventBus, NullLogger<AgentRouter>.Instance);
        var command = new ConfigCommand(router, pool, channels, registry);
        return (command, pool, channels, router);
    }

    private sealed class TestChannel(string channelType, string displayName, bool isConnected) : IChannel
    {
        public string ChannelType { get; } = channelType;
        public string DisplayName { get; } = displayName;
        public bool IsConnected { get; } = isConnected;

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

#pragma warning disable CS0067
        public event Func<ChannelMessage, Task>? MessageReceived;
#pragma warning restore CS0067
    }
}
