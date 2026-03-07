using JD.AI.Channels.Discord;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;

namespace JD.AI.Tests.Channels;

/// <summary>
/// Tests for DiscordChannel that don't require a live Discord connection.
/// External SDK calls (LoginAsync, StartAsync) are not testable without live credentials.
/// These tests validate construction, state properties, constants, and registration hooks.
/// </summary>
public sealed class DiscordChannelTests
{
    [Fact]
    public void Constructor_WithToken_DoesNotThrow()
    {
        var ex = Record.Exception(() => new DiscordChannel("fake-bot-token"));
        Assert.Null(ex);
    }

    [Fact]
    public void ChannelType_IsDiscord()
    {
        var channel = new DiscordChannel("token");
        Assert.Equal("discord", channel.ChannelType);
    }

    [Fact]
    public void DisplayName_IsDiscord()
    {
        var channel = new DiscordChannel("token");
        Assert.Equal("Discord", channel.DisplayName);
    }

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var channel = new DiscordChannel("token");
        Assert.False(channel.IsConnected);
    }

    [Fact]
    public void CommandPrefix_HasExpectedValue()
    {
        Assert.Equal("jdai-", DiscordChannel.CommandPrefix);
    }

    [Fact]
    public void ImplementsIChannel()
    {
        var channel = new DiscordChannel("token");
        Assert.IsAssignableFrom<IChannel>(channel);
    }

    [Fact]
    public void ImplementsICommandAwareChannel()
    {
        var channel = new DiscordChannel("token");
        Assert.IsAssignableFrom<ICommandAwareChannel>(channel);
    }

    [Fact]
    public async Task RegisterCommandsAsync_DoesNotThrow()
    {
        var channel = new DiscordChannel("token");
        var registry = Substitute.For<ICommandRegistry>();

        var ex = await Record.ExceptionAsync(() => channel.RegisterCommandsAsync(registry));
        Assert.Null(ex);
    }

    [Fact]
    public void MessageReceived_CanSubscribeAndUnsubscribe()
    {
        var channel = new DiscordChannel("token");
        channel.MessageReceived += HandleMessage;
        channel.MessageReceived -= HandleMessage;
        return;

        static Task HandleMessage(ChannelMessage _) => Task.CompletedTask;
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var channel = new DiscordChannel("token");
        var ex = await Record.ExceptionAsync(() => channel.DisconnectAsync());
        Assert.Null(ex);
    }
}
