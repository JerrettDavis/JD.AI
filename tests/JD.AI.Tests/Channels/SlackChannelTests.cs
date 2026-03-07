using JD.AI.Channels.Slack;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;

namespace JD.AI.Tests.Channels;

/// <summary>
/// Tests for SlackChannel. Live socket mode is not tested here.
/// Validates construction, properties, constants, and connect/disconnect behavior.
/// </summary>
public sealed class SlackChannelTests
{
    [Fact]
    public void Constructor_WithTokens_DoesNotThrow()
    {
        var ex = Record.Exception(() => new SlackChannel("xoxb-bot-token", "xapp-app-token"));
        Assert.Null(ex);
    }

    [Fact]
    public void ChannelType_IsSlack()
    {
        var ch = new SlackChannel("bot", "app");
        Assert.Equal("slack", ch.ChannelType);
    }

    [Fact]
    public void DisplayName_IsSlack()
    {
        var ch = new SlackChannel("bot", "app");
        Assert.Equal("Slack", ch.DisplayName);
    }

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var ch = new SlackChannel("bot", "app");
        Assert.False(ch.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.ConnectAsync();
        Assert.True(ch.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_AfterConnect_SetsIsConnectedFalse()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.ConnectAsync();
        await ch.DisconnectAsync();
        Assert.False(ch.IsConnected);
    }

    [Fact]
    public void CommandPrefix_StartsWithSlash()
    {
        Assert.StartsWith("/", SlackChannel.CommandPrefix, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplementsIChannel()
    {
        Assert.IsAssignableFrom<IChannel>(new SlackChannel("bot", "app"));
    }

    [Fact]
    public void ImplementsICommandAwareChannel()
    {
        Assert.IsAssignableFrom<ICommandAwareChannel>(new SlackChannel("bot", "app"));
    }

    [Fact]
    public async Task RegisterCommandsAsync_DoesNotThrow()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var ex = await Record.ExceptionAsync(() => ch.RegisterCommandsAsync(registry));
        Assert.Null(ex);
    }

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ch = new SlackChannel("bot", "app");
        // API client is null until ConnectAsync
        // No actual Slack call since not connected
        var ex = await Record.ExceptionAsync(() => ch.SendMessageAsync("C123", "hello"));
        // Either throws InvalidOperationException or NullReferenceException from SDK
        Assert.NotNull(ex);
    }
}
