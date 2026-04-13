using Discord;
using FluentAssertions;
using JD.AI.Channels.Discord;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;

namespace JD.AI.Tests.Channels.Discord;

/// <summary>
/// Unit tests for <see cref="DiscordChannel"/>.
/// Tests that require a live Discord connection (ConnectAsync, OnMessageReceived,
/// OnSlashCommandExecuted) are excluded because Discord.Net socket types are sealed
/// and cannot be instantiated or mocked without a real gateway connection.
/// All testable construction, property, lifecycle, and command-registration logic is covered.
/// </summary>
public sealed class DiscordChannelTests
{
    // ── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithToken_DoesNotThrow()
    {
        var ex = Record.Exception(() => new DiscordChannel("fake-bot-token"));

        ex.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyToken_DoesNotThrow()
    {
        // DiscordChannel stores the token; validation only happens at ConnectAsync
        var ex = Record.Exception(() => new DiscordChannel(string.Empty));

        ex.Should().BeNull();
    }

    // ── Constants ───────────────────────────────────────────────────────────

    [Fact]
    public void CommandPrefix_IsJdaiDash()
    {
        DiscordChannel.CommandPrefix.Should().Be("jdai-");
    }

    [Fact]
    public void CommandPrefix_StartsWithJdai()
    {
        DiscordChannel.CommandPrefix.Should().StartWith("jdai");
    }

    // ── Properties before connection ────────────────────────────────────────

    [Fact]
    public void ChannelType_IsDiscord()
    {
        var ch = new DiscordChannel("token");

        ch.ChannelType.Should().Be("discord");
    }

    [Fact]
    public void DisplayName_IsDiscord()
    {
        var ch = new DiscordChannel("token");

        ch.DisplayName.Should().Be("Discord");
    }

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var ch = new DiscordChannel("token");

        ch.IsConnected.Should().BeFalse();
    }

    // ── Interface implementation ────────────────────────────────────────────

    [Fact]
    public void ImplementsIChannel()
    {
        var ch = new DiscordChannel("token");

        ch.Should().BeAssignableTo<JD.AI.Core.Channels.IChannel>();
    }

    [Fact]
    public void ImplementsICommandAwareChannel()
    {
        var ch = new DiscordChannel("token");

        ch.Should().BeAssignableTo<ICommandAwareChannel>();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        var ch = new DiscordChannel("token");

        ch.Should().BeAssignableTo<IAsyncDisposable>();
    }

    // ── MessageReceived event ───────────────────────────────────────────────

    [Fact]
    public void MessageReceived_CanSubscribe()
    {
        var ch = new DiscordChannel("token");

        static Task Handler(ChannelMessage _) => Task.CompletedTask;

        var ex = Record.Exception(() => ch.MessageReceived += Handler);

        ex.Should().BeNull();
    }

    [Fact]
    public void MessageReceived_CanUnsubscribe()
    {
        var ch = new DiscordChannel("token");

        static Task Handler(ChannelMessage _) => Task.CompletedTask;

        ch.MessageReceived += Handler;

        var ex = Record.Exception(() => ch.MessageReceived -= Handler);

        ex.Should().BeNull();
    }

    [Fact]
    public void MessageReceived_InitiallyHasNoSubscribers_DoesNotThrow()
    {
        // Subscribing and immediately unsubscribing should be no-op safe
        var ch = new DiscordChannel("token");
        static Task Handler(ChannelMessage _) => Task.CompletedTask;

        ch.MessageReceived -= Handler; // unsubscribe without subscribing first

        // No exception means success
    }

    [Fact]
    public void MessageReceived_CanSubscribeMultipleHandlers()
    {
        var ch = new DiscordChannel("token");

        static Task HandlerA(ChannelMessage _) => Task.CompletedTask;
        static Task HandlerB(ChannelMessage _) => Task.CompletedTask;

        var ex = Record.Exception(() =>
        {
            ch.MessageReceived += HandlerA;
            ch.MessageReceived += HandlerB;
            ch.MessageReceived -= HandlerA;
            ch.MessageReceived -= HandlerB;
        });

        ex.Should().BeNull();
    }

    // ── DisconnectAsync before connect ──────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_WhenNeverConnected_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(() => ch.DisconnectAsync());

        ex.Should().BeNull();
    }

    [Fact]
    public async Task DisconnectAsync_CalledTwiceWithoutConnect_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(async () =>
        {
            await ch.DisconnectAsync();
            await ch.DisconnectAsync();
        });

        ex.Should().BeNull();
    }

    // ── DisposeAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_WhenNeverConnected_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(async () => await ch.DisposeAsync());

        ex.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimesWithoutConnect()
    {
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(async () =>
        {
            await ch.DisposeAsync();
            await ch.DisposeAsync();
        });

        ex.Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_UsingAwaitUsing_WhenNeverConnected_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(async () =>
        {
            await using var ch = new DiscordChannel("token");
            // No ConnectAsync call — dispose should be safe
        });

        ex.Should().BeNull();
    }

    // ── SendMessageAsync before connect ─────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(() => ch.SendMessageAsync("123456789", "hello"));

        ex.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ExceptionMessageMentionsConnection()
    {
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(() => ch.SendMessageAsync("channel-id", "content"));

        ex.Should().BeOfType<InvalidOperationException>();
        ex!.Message.Should().Contain("connected", Exactly.Once(), because: "the error should indicate the channel is not connected");
    }

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ch = new DiscordChannel("token");
        using var cts = new CancellationTokenSource();

        var ex = await Record.ExceptionAsync(() => ch.SendMessageAsync("channel-id", "content", cts.Token));

        ex.Should().BeOfType<InvalidOperationException>();
    }

    // ── RegisterCommandsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RegisterCommandsAsync_WhenNotConnected_DoesNotThrow()
    {
        // When not connected, RegisterCommandsAsync should store the registry
        // but skip BulkOverwriteGlobalApplicationCommandsAsync
        var ch = new DiscordChannel("token");
        var registry = Substitute.For<ICommandRegistry>();
        registry.Commands.Returns(new List<IChannelCommand>());

        var ex = await Record.ExceptionAsync(() => ch.RegisterCommandsAsync(registry));

        ex.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCommandsAsync_WithNullRegistry_DoesNotThrow()
    {
        // Passing null is allowed by the interface signature at compile time
        // (ICommandRegistry is a reference type with no null guard in DiscordChannel)
        var ch = new DiscordChannel("token");

        var ex = await Record.ExceptionAsync(() => ch.RegisterCommandsAsync(null!));

        // DiscordChannel stores whatever is passed and early-returns; no crash expected
        ex.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCommandsAsync_WithEmptyCommandList_WhenNotConnected_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");
        var registry = new CommandRegistry(); // concrete implementation, zero commands

        var ex = await Record.ExceptionAsync(() => ch.RegisterCommandsAsync(registry));

        ex.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCommandsAsync_CanBeCalledMultipleTimes_WhenNotConnected()
    {
        var ch = new DiscordChannel("token");
        var registry = Substitute.For<ICommandRegistry>();
        registry.Commands.Returns(new List<IChannelCommand>());

        var ex = await Record.ExceptionAsync(async () =>
        {
            await ch.RegisterCommandsAsync(registry);
            await ch.RegisterCommandsAsync(registry);
        });

        ex.Should().BeNull();
    }

    // ── CommandPrefix semantics ─────────────────────────────────────────────

    [Fact]
    public void CommandPrefix_Length_IsGreaterThanZero()
    {
        DiscordChannel.CommandPrefix.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CommandPrefix_DoesNotContainUpperCase()
    {
        // Discord slash command names must be lowercase — every char should be non-uppercase
        var hasUppercase = DiscordChannel.CommandPrefix.Any(char.IsUpper);
        hasUppercase.Should().BeFalse("Discord slash command names must be lowercase");
    }

    [Fact]
    public void CommandPrefix_DoesNotContainSpaces()
    {
        DiscordChannel.CommandPrefix.Should().NotContain(" ",
            because: "Discord slash command names cannot contain spaces");
    }

    // ── ChannelType correctness ─────────────────────────────────────────────

    [Fact]
    public void ChannelType_IsLowercase()
    {
        var ch = new DiscordChannel("token");

        // Channel type identifiers are conventionally lowercase
        var hasUppercase = ch.ChannelType.Any(char.IsUpper);
        hasUppercase.Should().BeFalse("channel type identifiers are conventionally lowercase");
    }

    [Fact]
    public void ChannelType_DoesNotContainSpaces()
    {
        var ch = new DiscordChannel("token");

        ch.ChannelType.Should().NotContain(" ");
    }

    // ── Multiple instance independence ──────────────────────────────────────

    [Fact]
    public void MultipleInstances_HaveIndependentState()
    {
        var ch1 = new DiscordChannel("token-1");
        var ch2 = new DiscordChannel("token-2");

        // IsConnected is per-instance; neither should be affected by the other
        ch1.IsConnected.Should().BeFalse();
        ch2.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void MultipleInstances_MessageReceived_AreIndependent()
    {
        var ch1 = new DiscordChannel("token-1");
        var ch2 = new DiscordChannel("token-2");

        var ch1Invoked = false;

        ch1.MessageReceived += _ =>
        {
            ch1Invoked = true;
            return Task.CompletedTask;
        };

        // ch2 has no handler — subscribing to ch1 should not affect ch2
        ch1.IsConnected.Should().BeFalse();
        ch2.IsConnected.Should().BeFalse();
        ch1Invoked.Should().BeFalse("no message was ingested");
    }

    // ── Cancellation token handling ─────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_WithCancelledToken_WhenNeverConnected_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // DisconnectAsync with a cancelled token when client is null should not throw
        var ex = await Record.ExceptionAsync(() => ch.DisconnectAsync(cts.Token));

        ex.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCommandsAsync_WithCancelledToken_WhenNotConnected_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");
        var registry = Substitute.For<ICommandRegistry>();
        registry.Commands.Returns(new List<IChannelCommand>());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // When not connected, RegisterCommandsAsync returns early without doing async work
        var ex = await Record.ExceptionAsync(() => ch.RegisterCommandsAsync(registry, cts.Token));

        ex.Should().BeNull();
    }

    // ── Static helper method tests (Truncate) ────────────────────────────────────

    [Fact]
    public void Truncate_ValueShorterThanMax_ReturnsOriginal()
    {
        var value = "short";
        var method = typeof(DiscordChannel).GetMethod("Truncate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = (string?)method!.Invoke(null, new object[] { value, 50 });

        result.Should().Be("short");
    }

    [Fact]
    public void Truncate_ValueLongerThanMax_TruncatesWithEllipsis()
    {
        var value = "this is a very long string that exceeds the maximum length";
        var method = typeof(DiscordChannel).GetMethod("Truncate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = (string?)method!.Invoke(null, new object[] { value, 20 });

        result.Should().NotBeNull();
        result.Should().HaveLength(20);
        result.Should().EndWith("…");
        result.Should().NotContain("exceeds");
    }

    [Fact]
    public void Truncate_ValueEqualToMax_ReturnsOriginal()
    {
        var value = "exact";
        var method = typeof(DiscordChannel).GetMethod("Truncate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = (string?)method!.Invoke(null, new object[] { value, 5 });

        result.Should().Be("exact");
    }

    // ── ConfirmToolCallAsync additional paths ──────────────────────────────────

    [Fact]
    public async Task ConfirmToolCallAsync_WhenPrivilegedUsersEmpty_AutoApproves()
    {
        var ch = new DiscordChannel("token", privilegedUserIds: new List<ulong>());
        var method = typeof(DiscordChannel).GetMethod("ConfirmToolCallAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<bool>?)method!.Invoke(ch, new object?[] { "test-tool", null });

        Assert.NotNull(task);
        var result = await task!;
        result.Should().BeTrue("should auto-approve when no privileged users configured");
    }

    [Fact]
    public async Task ConfirmToolCallAsync_WithOnlyOneTool_AutoApproves()
    {
        // When _privilegedUserIds is empty, ConfirmToolCallAsync should auto-approve
        var ch = new DiscordChannel("token");
        var method = typeof(DiscordChannel).GetMethod("ConfirmToolCallAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<bool>?)method!.Invoke(ch, new object?[] { "single-tool", "{\"param\": \"value\"}" });

        Assert.NotNull(task);
        var result = await task!;
        result.Should().BeTrue();
    }

    // ── IAgentOutput no-op method tests ────────────────────────────────────────

    [Fact]
    public void IAgentOutput_RenderInfo_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");

        var ex = Record.Exception(() => ch.RenderInfo("test message"));

        ex.Should().BeNull();
    }

    [Fact]
    public void IAgentOutput_RenderWarning_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");

        var ex = Record.Exception(() => ch.RenderWarning("warning message"));

        ex.Should().BeNull();
    }

    [Fact]
    public void IAgentOutput_RenderError_DoesNotThrow()
    {
        var ch = new DiscordChannel("token");

        var ex = Record.Exception(() => ch.RenderError("error message"));

        ex.Should().BeNull();
    }
}
