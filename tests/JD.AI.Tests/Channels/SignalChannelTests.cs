using JD.AI.Channels.Signal;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;

namespace JD.AI.Tests.Channels;

/// <summary>
/// Tests for SignalChannel that don't require a live signal-cli daemon.
/// The signal-cli process is not started in tests — only construction, state,
/// constants, and message-routing logic are validated.
/// </summary>
public sealed class SignalChannelTests
{
    [Fact]
    public void Constructor_DefaultCliPath_DoesNotThrow()
    {
        var ex = Record.Exception(() => new SignalChannel("+11234567890"));
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_CustomCliPath_DoesNotThrow()
    {
        var ex = Record.Exception(() => new SignalChannel("+11234567890", "/usr/local/bin/signal-cli"));
        Assert.Null(ex);
    }

    [Fact]
    public void ChannelType_IsSignal()
    {
        var ch = new SignalChannel("+11234567890");
        Assert.Equal("signal", ch.ChannelType);
    }

    [Fact]
    public void DisplayName_ContainsAccount()
    {
        var ch = new SignalChannel("+11234567890");
        Assert.Contains("+11234567890", ch.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var ch = new SignalChannel("+11234567890");
        Assert.False(ch.IsConnected);
    }

    [Fact]
    public void CommandPrefix_StartsWithBang()
    {
        Assert.StartsWith("!", SignalChannel.CommandPrefix, StringComparison.Ordinal);
    }

    [Fact]
    public void SlashPrefix_StartsWithSlash()
    {
        Assert.StartsWith("/", SignalChannel.SlashPrefix, StringComparison.Ordinal);
    }

    [Fact]
    public void ImplementsIChannel()
    {
        Assert.IsAssignableFrom<IChannel>(new SignalChannel("+11234567890"));
    }

    [Fact]
    public void ImplementsICommandAwareChannel()
    {
        Assert.IsAssignableFrom<ICommandAwareChannel>(new SignalChannel("+11234567890"));
    }

    [Fact]
    public async Task RegisterCommandsAsync_DoesNotThrow()
    {
        var ch = new SignalChannel("+11234567890");
        var registry = Substitute.For<ICommandRegistry>();
        var ex = await Record.ExceptionAsync(() => ch.RegisterCommandsAsync(registry));
        Assert.Null(ex);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new SignalChannel("+11234567890");
        var ex = await Record.ExceptionAsync(() => ch.DisconnectAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new SignalChannel("+11234567890");
        var ex = await Record.ExceptionAsync(async () => await ch.DisposeAsync());
        Assert.Null(ex);
    }
}
