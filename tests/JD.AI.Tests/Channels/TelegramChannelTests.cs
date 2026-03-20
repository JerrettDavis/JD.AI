using JD.AI.Channels.Telegram;
using JD.AI.Core.Channels;
using System.Reflection;
using Telegram.Bot.Types;

namespace JD.AI.Tests.Channels;

// A Telegram bot token must be in the format: {number}:{alphanumeric}
// Using a valid-format fake token for tests that call ConnectAsync.
file static class FakeTelegramToken
{
    public const string Valid = "1234567890:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw";
    public const string Any = "token"; // For tests that don't call ConnectAsync
}

/// <summary>
/// Tests for TelegramChannel. No live bot token is used.
/// Validates construction, state properties, and lifecycle transitions.
/// </summary>
public sealed class TelegramChannelTests
{
    [Fact]
    public void Constructor_WithToken_DoesNotThrow()
    {
        var ex = Record.Exception(() => new TelegramChannel(FakeTelegramToken.Any));
        Assert.Null(ex);
    }

    [Fact]
    public void ChannelType_IsTelegram()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        Assert.Equal("telegram", ch.ChannelType);
    }

    [Fact]
    public void DisplayName_IsTelegram()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        Assert.Equal("Telegram", ch.DisplayName);
    }

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        Assert.False(ch.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        // TelegramBotClient validates the token format at construction.
        // Use a valid-format token (number:base64-like) so the constructor doesn't throw.
        var ch = new TelegramChannel(FakeTelegramToken.Valid);
        await ch.ConnectAsync();
        Assert.True(ch.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_AfterConnect_SetsIsConnectedFalse()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Valid);
        await ch.ConnectAsync();
        await ch.DisconnectAsync();
        Assert.False(ch.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        var ex = await Record.ExceptionAsync(() => ch.DisconnectAsync());
        Assert.Null(ex);
    }

    [Fact]
    public void ImplementsIChannel()
    {
        Assert.IsAssignableFrom<IChannel>(new TelegramChannel(FakeTelegramToken.Any));
    }

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        await using var ch = new TelegramChannel(FakeTelegramToken.Any);
        // Disposing without connecting should be safe
    }

    [Fact]
    public async Task DisposeAsync_AfterConnect_DoesNotThrow()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Valid);
        await ch.ConnectAsync();
        var ex = await Record.ExceptionAsync(async () => await ch.DisposeAsync());
        Assert.Null(ex);
    }

    [Fact]
    public void MessageReceived_CanSubscribeAndUnsubscribe()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        ch.MessageReceived += HandleMessage;
        ch.MessageReceived -= HandleMessage;
        return;

        static Task HandleMessage(ChannelMessage _) => Task.CompletedTask;
    }

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ch.SendMessageAsync("12345", "hello"));
    }

    [Fact]
    public async Task SendMessageAsync_WithInvalidConversationId_ThrowsFormatException()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Valid);
        await ch.ConnectAsync();

        await Assert.ThrowsAsync<FormatException>(() =>
            ch.SendMessageAsync("not-a-number", "hello"));
    }

    [Fact]
    public async Task HandleUpdateAsync_WithText_RaisesMessageReceived()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);

        ChannelMessage? received = null;
        ch.MessageReceived += message =>
        {
            received = message;
            return Task.CompletedTask;
        };

        var update = new Update
        {
            Message = new Message
            {
                Id = 99,
                Text = "hello from telegram",
                Date = DateTime.SpecifyKind(new DateTime(2026, 3, 20, 12, 0, 0), DateTimeKind.Utc),
                Chat = new Chat { Id = 42 },
                From = new User { Id = 7, FirstName = "JD" },
                MessageThreadId = 123
            }
        };

        var method = typeof(TelegramChannel).GetMethod(
            "HandleUpdateAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task?)method!.Invoke(ch, [null!, update, CancellationToken.None]);
        Assert.NotNull(task);
        await task!;

        Assert.NotNull(received);
        Assert.Equal("99", received!.Id);
        Assert.Equal("42", received.ChannelId);
        Assert.Equal("7", received.SenderId);
        Assert.Equal("JD", received.SenderDisplayName);
        Assert.Equal("hello from telegram", received.Content);
        Assert.Equal("123", received.ThreadId);
    }

    [Fact]
    public async Task HandleUpdateAsync_WithoutText_DoesNotRaiseMessageReceived()
    {
        var ch = new TelegramChannel(FakeTelegramToken.Any);
        var invoked = false;
        ch.MessageReceived += _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var update = new Update
        {
            Message = new Message
            {
                Id = 1,
                Chat = new Chat { Id = 1 },
                Date = DateTime.UtcNow
            }
        };

        var method = typeof(TelegramChannel).GetMethod(
            "HandleUpdateAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = (Task?)method!.Invoke(ch, [null!, update, CancellationToken.None]);
        Assert.NotNull(task);
        await task!;

        Assert.False(invoked);
    }
}
