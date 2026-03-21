using System.Reflection;
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
    public async Task RegisterCommandsAsync_WhenNotConnected_DoesNotTouchRegistry()
    {
        var channel = new DiscordChannel("token");
        var registry = Substitute.For<ICommandRegistry>();

        await channel.RegisterCommandsAsync(registry);

        Assert.Empty(registry.ReceivedCalls());
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

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var channel = new DiscordChannel("token");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            channel.SendMessageAsync("123", "hello"));
    }

    [Fact]
    public async Task SendMessageAsync_WithNonNumericConversationId_DoesNotThrow()
    {
        var channel = new DiscordChannel("token");

        var field = typeof(DiscordChannel).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(channel, new global::Discord.WebSocket.DiscordSocketClient());

        var ex = await Record.ExceptionAsync(() =>
            channel.SendMessageAsync("not-a-number", "hello"));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(CommandParameterType.Text, "String")]
    [InlineData(CommandParameterType.Number, "Integer")]
    [InlineData(CommandParameterType.Boolean, "Boolean")]
    public void MapParameterType_MapsExpectedDiscordOptionType(CommandParameterType type, string expected)
    {
        var method = typeof(DiscordChannel).GetMethod("MapParameterType", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [type]);

        Assert.NotNull(result);
        Assert.Equal(expected, result!.ToString());
    }

    [Fact]
    public void MapParameterType_UnknownValue_FallsBackToString()
    {
        var method = typeof(DiscordChannel).GetMethod("MapParameterType", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [(CommandParameterType)999]);

        Assert.NotNull(result);
        Assert.Equal("String", result!.ToString());
    }

    [Fact]
    public void Truncate_WhenValueWithinMax_ReturnsOriginal()
    {
        var method = typeof(DiscordChannel).GetMethod("Truncate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var value = "short";
        var result = (string?)method!.Invoke(null, [value, 10]);

        Assert.Equal(value, result);
    }

    [Fact]
    public void Truncate_WhenValueEqualsMaxLength_ReturnsOriginal()
    {
        var method = typeof(DiscordChannel).GetMethod("Truncate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var value = "1234567890";
        var result = (string?)method!.Invoke(null, [value, 10]);

        Assert.Equal(value, result);
    }

    [Fact]
    public void Truncate_WhenValueExceedsMax_AddsEllipsis()
    {
        var method = typeof(DiscordChannel).GetMethod("Truncate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = (string?)method!.Invoke(null, ["abcdefghijklmnopqrstuvwxyz", 10]);

        Assert.Equal("abcdefghi…", result);
    }
}
