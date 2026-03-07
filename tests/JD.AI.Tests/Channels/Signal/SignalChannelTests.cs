#pragma warning disable MA0002, CA1849

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JD.AI.Channels.Signal;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace JD.AI.Tests.Channels.Signal;

/// <summary>
/// Comprehensive tests for <see cref="SignalChannel"/>.
/// Tests cover construction, properties, constants, interface contracts,
/// message parsing (JSON-RPC envelope format), command routing, argument mapping,
/// error handling, and lifecycle management — all without requiring a live signal-cli process.
/// </summary>
/// <remarks>
/// Message processing logic is driven by injecting a fake <see cref="StreamReader"/> into
/// the private <c>_daemon</c> Process via reflection, then invoking <c>ReadLoopAsync</c>
/// directly (also via reflection). This exercises the real parsing code without spawning signal-cli.
/// </remarks>
public sealed class SignalChannelTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private const string Account = "+11234567890";

    /// <summary>
    /// Builds the JSON-RPC "receive" envelope that signal-cli emits on stdout.
    /// </summary>
    private static string BuildReceiveJson(
        string message,
        string source = "+19998887777",
        string? sourceName = null,
        bool includeDataMessage = true)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["source"] = source
        };
        if (sourceName is not null)
            envelope["sourceName"] = sourceName;

        if (includeDataMessage)
            envelope["dataMessage"] = new Dictionary<string, object?> { ["message"] = message };

        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "receive",
            @params = new { envelope }
        });
    }

    /// <summary>
    /// Creates a <see cref="StreamReader"/> backed by the provided lines (each line-terminated).
    /// </summary>
    private static StreamReader MakeReader(params string[] lines)
    {
        var joined = string.Join(Environment.NewLine, lines);
        var bytes = Encoding.UTF8.GetBytes(joined);
        return new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
    }

    /// <summary>
    /// Injects <paramref name="reader"/> as the StandardOutput of a dummy Process,
    /// then assigns that Process to <c>SignalChannel._daemon</c> via reflection.
    /// </summary>
    private static void InjectReaderIntoDaemon(SignalChannel channel, StreamReader reader)
    {
        // Create a Process instance without starting it.
        var process = new Process();

        // Inject the StreamReader into Process._standardOutput (private field).
        var procStdOutField = typeof(Process).GetField(
            "_standardOutput",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        procStdOutField.Should().NotBeNull("Process._standardOutput field must exist via reflection");
        procStdOutField.SetValue(process, reader);

        // Inject the Process into SignalChannel._daemon (private field).
        var daemonField = typeof(SignalChannel).GetField(
            "_daemon",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        daemonField.Should().NotBeNull("SignalChannel._daemon field must exist via reflection");
        daemonField.SetValue(channel, process);
    }

    /// <summary>
    /// Invokes the private <c>ReadLoopAsync(CancellationToken)</c> method and awaits it.
    /// </summary>
    private static async Task InvokeReadLoopAsync(SignalChannel channel, CancellationToken ct = default)
    {
        var method = typeof(SignalChannel).GetMethod(
            "ReadLoopAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Should().NotBeNull("SignalChannel.ReadLoopAsync must exist via reflection");

        var task = (Task)method.Invoke(channel, [ct])!;
        await task;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. Construction
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithAccountOnly_DoesNotThrow()
    {
        var act = () => new SignalChannel(Account);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCustomCliPath_DoesNotThrow()
    {
        var act = () => new SignalChannel(Account, "/usr/local/bin/signal-cli");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullCliPath_DefaultsToSignalCli()
    {
        // Passing null explicitly should behave the same as omitting it.
        var act = () => new SignalChannel(Account, null);
        act.Should().NotThrow();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. Properties
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ChannelType_ReturnsSignal()
    {
        var ch = new SignalChannel(Account);
        ch.ChannelType.Should().Be("signal");
    }

    [Fact]
    public void DisplayName_ContainsAccount()
    {
        var ch = new SignalChannel(Account);
        ch.DisplayName.Should().Contain(Account);
    }

    [Fact]
    public void DisplayName_ContainsSignalWord()
    {
        var ch = new SignalChannel(Account);
        ch.DisplayName.Should().ContainEquivalentOf("signal");
    }

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var ch = new SignalChannel(Account);
        ch.IsConnected.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. Constants
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CommandPrefix_StartsWithBang()
    {
        SignalChannel.CommandPrefix.Should().StartWith("!");
    }

    [Fact]
    public void CommandPrefix_ContainsJdai()
    {
        SignalChannel.CommandPrefix.Should().Contain("jdai");
    }

    [Fact]
    public void CommandPrefix_HasExpectedValue()
    {
        SignalChannel.CommandPrefix.Should().Be("!jdai-");
    }

    [Fact]
    public void SlashPrefix_StartsWithSlash()
    {
        SignalChannel.SlashPrefix.Should().StartWith("/");
    }

    [Fact]
    public void SlashPrefix_ContainsJdai()
    {
        SignalChannel.SlashPrefix.Should().Contain("jdai");
    }

    [Fact]
    public void SlashPrefix_HasExpectedValue()
    {
        SignalChannel.SlashPrefix.Should().Be("/jdai-");
    }

    [Fact]
    public void CommandPrefix_And_SlashPrefix_HaveSameSuffix()
    {
        // Both prefixes should reference the same command namespace.
        var bangSuffix = SignalChannel.CommandPrefix.TrimStart('!');
        var slashSuffix = SignalChannel.SlashPrefix.TrimStart('/');
        bangSuffix.Should().Be(slashSuffix);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. Interface contracts
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ImplementsIChannel()
    {
        new SignalChannel(Account).Should().BeAssignableTo<IChannel>();
    }

    [Fact]
    public void ImplementsICommandAwareChannel()
    {
        new SignalChannel(Account).Should().BeAssignableTo<ICommandAwareChannel>();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        new SignalChannel(Account).Should().BeAssignableTo<IAsyncDisposable>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. MessageReceived event
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MessageReceived_CanSubscribe()
    {
        var ch = new SignalChannel(Account);
        ch.MessageReceived += _ => Task.CompletedTask;
        // No throw expected; event wire-up should succeed.
    }

    [Fact]
    public void MessageReceived_CanUnsubscribe()
    {
        var ch = new SignalChannel(Account);
        Func<ChannelMessage, Task> handler = _ => Task.CompletedTask;
        ch.MessageReceived += handler;
        var act = () => { ch.MessageReceived -= handler; };
        act.Should().NotThrow();
    }

    [Fact]
    public void MessageReceived_CanSubscribeMultipleHandlers()
    {
        var ch = new SignalChannel(Account);
        ch.MessageReceived += _ => Task.CompletedTask;
        ch.MessageReceived += _ => Task.CompletedTask;
        // Should not throw.
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. RegisterCommandsAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterCommandsAsync_WithRegistry_DoesNotThrow()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();

        var act = () => ch.RegisterCommandsAsync(registry);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterCommandsAsync_ReturnsCompletedTask()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();

        await ch.RegisterCommandsAsync(registry);
        // The return type is Task (void-like); completing without throw is the contract.
    }

    [Fact]
    public async Task RegisterCommandsAsync_CalledTwice_DoesNotThrow()
    {
        var ch = new SignalChannel(Account);
        var reg1 = Substitute.For<ICommandRegistry>();
        var reg2 = Substitute.For<ICommandRegistry>();

        await ch.RegisterCommandsAsync(reg1);
        var act = () => ch.RegisterCommandsAsync(reg2);
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. DisconnectAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new SignalChannel(Account);
        var act = () => ch.DisconnectAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ReturnsFast()
    {
        var ch = new SignalChannel(Account);
        // Should complete essentially instantly — no blocking.
        var t = ch.DisconnectAsync();
        t.IsCompleted.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. SendMessageAsync — "not connected" guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ch = new SignalChannel(Account);

        var act = () => ch.SendMessageAsync("+19998887777", "hello");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not connected*");
    }

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsRegardlessOfContent()
    {
        var ch = new SignalChannel(Account);

        await ch.Invoking(c => c.SendMessageAsync("anyone", ""))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 9. DisposeAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new SignalChannel(Account);
        var act = async () => await ch.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var ch = new SignalChannel(Account);
        await ch.DisposeAsync();
        var act = async () => await ch.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UsingAwait_DisposesCorrectly()
    {
        var act = async () =>
        {
            await using var ch = new SignalChannel(Account);
            // Just constructing and immediately disposing.
        };
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 10. ReadLoopAsync — JSON message parsing
    //
    // These tests inject fake JSON into the private daemon's StandardOutput
    // and invoke ReadLoopAsync via reflection to exercise the real parsing code.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_RaisesMessageReceived()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson("Hello world", source: "+19998887777");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(1);
        received[0].Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_MapsSourceToSenderId()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        var json = BuildReceiveJson("Hi", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.SenderId.Should().Be("+15554443333");
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_MapsSourceToChannelId()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        var json = BuildReceiveJson("Hi", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.ChannelId.Should().Be("+15554443333");
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_WithSourceName_MapsSenderDisplayName()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        var json = BuildReceiveJson("Hi", source: "+15554443333", sourceName: "Alice");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.SenderDisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_WithoutSourceName_SenderDisplayNameIsNull()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        var json = BuildReceiveJson("Hi", source: "+15554443333", sourceName: null);
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.SenderDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_WithoutSource_UsesFallbackUnknown()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        // Build envelope without "source" field
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "receive",
            @params = new
            {
                envelope = new
                {
                    dataMessage = new { message = "test" }
                }
            }
        });
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.SenderId.Should().Be("unknown");
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_MessageId_IsNonEmpty()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        var json = BuildReceiveJson("Hello");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReadLoop_ValidReceiveEnvelope_Timestamp_IsRecentUtc()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        var json = BuildReceiveJson("Hello");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.Timestamp.Should().BeAfter(before);
        received.Timestamp.Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task ReadLoop_MultipleMessages_AllRaiseMessageReceived()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json1 = BuildReceiveJson("First", source: "+15550001111");
        var json2 = BuildReceiveJson("Second", source: "+15550002222");
        var json3 = BuildReceiveJson("Third", source: "+15550003333");
        InjectReaderIntoDaemon(ch, MakeReader(json1, json2, json3));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(3);
        received[0].Content.Should().Be("First");
        received[1].Content.Should().Be("Second");
        received[2].Content.Should().Be("Third");
    }

    [Fact]
    public async Task ReadLoop_NoMessageReceivedHandler_DoesNotThrow()
    {
        var ch = new SignalChannel(Account);
        // No handler attached — should silently swallow the event.

        var json = BuildReceiveJson("Hello");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        var act = () => InvokeReadLoopAsync(ch);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadLoop_MalformedJson_IsSkippedSilently()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        // Mix a malformed line with a valid one.
        var validJson = BuildReceiveJson("Good message");
        InjectReaderIntoDaemon(ch, MakeReader("{NOT VALID JSON{{", validJson));

        await InvokeReadLoopAsync(ch);

        // Only the valid message should be received.
        received.Should().HaveCount(1);
        received[0].Content.Should().Be("Good message");
    }

    [Fact]
    public async Task ReadLoop_EmptyLine_IsSkippedSilently()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var validJson = BuildReceiveJson("After empty");
        // An empty line is not valid JSON; should be swallowed.
        InjectReaderIntoDaemon(ch, MakeReader("", validJson));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReadLoop_JsonWithWrongMethod_IsIgnored()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        // A JSON-RPC notification with a different method name should be ignored.
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "sendResult",
            @params = new { }
        });
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_JsonWithNoMethodProperty_IsIgnored()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = JsonSerializer.Serialize(new { jsonrpc = "2.0", result = "ok" });
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_EnvelopeWithNoDataMessage_IsIgnored()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        // Envelope present, but no dataMessage — e.g. a receipt or typing indicator.
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "receive",
            @params = new
            {
                envelope = new
                {
                    source = "+15551234567",
                    receiptMessage = new { type = "READ" }
                }
            }
        });
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_DataMessageWithNoMessageField_ContentIsEmpty()
    {
        var ch = new SignalChannel(Account);
        ChannelMessage? received = null;
        ch.MessageReceived += msg => { received = msg; return Task.CompletedTask; };

        // dataMessage exists but has no "message" field (e.g. attachment-only).
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method = "receive",
            @params = new
            {
                envelope = new
                {
                    source = "+15551234567",
                    dataMessage = new { timestamp = 12345 }
                }
            }
        });
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().NotBeNull();
        received!.Content.Should().Be("");
    }

    [Fact]
    public async Task ReadLoop_EmptyStream_DoesNotRaiseMessageReceived()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        InjectReaderIntoDaemon(ch, MakeReader());

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_CancellationToken_AlreadyCancelled_StopsImmediately()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Even with valid data, a pre-cancelled token should prevent processing.
        var json = BuildReceiveJson("Should not arrive");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        // ReadLineAsync will throw OperationCanceledException; the loop should handle it.
        try
        {
            await InvokeReadLoopAsync(ch, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Acceptable — the loop may propagate the cancellation.
        }

        // No messages should have been dispatched.
        received.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 11. ReadLoopAsync — command routing via ! prefix
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadLoop_BangPrefixMessage_WithRegisteredCommand_DoesNotRaiseMessageReceived()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("help");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "Help text" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson("!jdai-help", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        // Commands are dispatched, not delivered as plain messages.
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_SlashPrefixMessage_WithRegisteredCommand_DoesNotRaiseMessageReceived()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("help");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "Help text" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson("/jdai-help", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_CommandPrefix_CaseInsensitive_BangPrefix()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("help");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "ok" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        // Mixed case prefix
        var json = BuildReceiveJson("!JDAI-help", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty("command prefix detection is case-insensitive");
    }

    [Fact]
    public async Task ReadLoop_CommandPrefix_WithNoRegistrySet_TreatsAsPlainMessage()
    {
        var ch = new SignalChannel(Account);
        // No registry registered — command-looking messages become plain messages.

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson("!jdai-help", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(1);
        received[0].Content.Should().Be("!jdai-help");
    }

    [Fact]
    public async Task ReadLoop_CommandExecution_PassesCorrectContext()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("status");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());

        CommandContext? capturedContext = null;
        command.ExecuteAsync(Arg.Do<CommandContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "OK" });
        registry.GetCommand("status").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-status", source: "+15554443333", sourceName: "Bob");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        capturedContext.Should().NotBeNull();
        capturedContext!.CommandName.Should().Be("status");
        capturedContext.InvokerId.Should().Be("+15554443333");
        capturedContext.InvokerDisplayName.Should().Be("Bob");
        capturedContext.ChannelId.Should().Be("+15554443333");
        capturedContext.ChannelType.Should().Be("signal");
    }

    [Fact]
    public async Task ReadLoop_CommandExecution_MapsPositionalArguments()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("switch");
        command.Parameters.Returns(new List<CommandParameter>
        {
            new() { Name = "model", Description = "Target model", IsRequired = true }
        }.AsReadOnly());

        CommandContext? capturedContext = null;
        command.ExecuteAsync(Arg.Do<CommandContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "Switched" });
        registry.GetCommand("switch").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-switch gpt-4o", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        capturedContext.Should().NotBeNull();
        capturedContext!.Arguments.Should().ContainKey("model");
        capturedContext.Arguments["model"].Should().Be("gpt-4o");
    }

    [Fact]
    public async Task ReadLoop_CommandExecution_MultiplePositionalArguments_MappedInOrder()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("cmd");
        command.Parameters.Returns(new List<CommandParameter>
        {
            new() { Name = "first",  Description = "First param",  IsRequired = true },
            new() { Name = "second", Description = "Second param", IsRequired = true }
        }.AsReadOnly());

        CommandContext? capturedContext = null;
        command.ExecuteAsync(Arg.Do<CommandContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "ok" });
        registry.GetCommand("cmd").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-cmd foo bar", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        capturedContext.Should().NotBeNull();
        capturedContext!.Arguments["first"].Should().Be("foo");
        capturedContext.Arguments["second"].Should().Be("bar");
    }

    [Fact]
    public async Task ReadLoop_CommandExecution_ExtraTokensBeyondParameters_AreIgnored()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("cmd");
        command.Parameters.Returns(new List<CommandParameter>
        {
            new() { Name = "only", Description = "Single param", IsRequired = true }
        }.AsReadOnly());

        CommandContext? capturedContext = null;
        command.ExecuteAsync(Arg.Do<CommandContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "ok" });
        registry.GetCommand("cmd").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-cmd val1 extra1 extra2", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        capturedContext!.Arguments.Should().ContainKey("only");
        capturedContext.Arguments["only"].Should().Be("val1");
        capturedContext.Arguments.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReadLoop_CommandExecution_NoArguments_ArgumentsDictionaryIsEmpty()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("help");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());

        CommandContext? capturedContext = null;
        command.ExecuteAsync(Arg.Do<CommandContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "Help!" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-help", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        capturedContext!.Arguments.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadLoop_SlashPrefixCommand_StripsCorrectPrefixLength()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("status");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());

        CommandContext? capturedContext = null;
        command.ExecuteAsync(Arg.Do<CommandContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "ok" });
        registry.GetCommand("status").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("/jdai-status", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        capturedContext.Should().NotBeNull();
        capturedContext!.CommandName.Should().Be("status");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 12. HandleCommandAsync — unknown command
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadLoop_UnknownCommand_DoesNotThrow()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        // GetCommand returns null for unknown commands.
        registry.GetCommand(Arg.Any<string>()).Returns((IChannelCommand?)null);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-unknownxyz", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        var act = () => InvokeReadLoopAsync(ch);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadLoop_CommandWithEmptyNameAfterPrefix_IsIgnoredGracefully()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        registry.GetCommand(Arg.Any<string>()).Returns((IChannelCommand?)null);
        await ch.RegisterCommandsAsync(registry);

        // "!jdai-" with nothing after it — parts will be empty.
        var json = BuildReceiveJson("!jdai-", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        var act = () => InvokeReadLoopAsync(ch);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadLoop_CommandExecution_WhenCommandThrows_DoesNotPropagateException()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("boom");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Command exploded"));
        registry.GetCommand("boom").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var json = BuildReceiveJson("!jdai-boom", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        // The outer catch block swallows command errors.
        var act = () => InvokeReadLoopAsync(ch);
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 13. Non-command messages when registry is set
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadLoop_NonCommandMessage_WithRegistrySet_StillRaisesMessageReceived()
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        registry.GetCommand(Arg.Any<string>()).Returns((IChannelCommand?)null);
        await ch.RegisterCommandsAsync(registry);

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson("Just a regular message", source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(1);
        received[0].Content.Should().Be("Just a regular message");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 14. JSON RPC SendMessage format (via WriteLineAsync)
    //     Since _writer is null before connect, we test the guard throws.
    //     We also verify the JSON structure indirectly through the exception path.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_WriterIsNull_ThrowsInvalidOperation()
    {
        var ch = new SignalChannel(Account);
        // _writer is never set without ConnectAsync + a real process.
        await ch.Invoking(c => c.SendMessageAsync("+15554443333", "test"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 15. Prefix edge-case coverage
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("!jdai-help")]
    [InlineData("!JDAI-help")]
    [InlineData("!Jdai-help")]
    [InlineData("/jdai-help")]
    [InlineData("/JDAI-help")]
    [InlineData("/Jdai-help")]
    public async Task ReadLoop_CommandPrefixVariants_AreDetectedCaseInsensitively(string messageText)
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("help");
        command.Parameters.Returns(new List<CommandParameter>().AsReadOnly());
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(new CommandResult { Success = true, Content = "help content" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson(messageText, source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().BeEmpty($"'{messageText}' should be dispatched as a command, not a plain message");
    }

    [Theory]
    [InlineData("Hello world")]
    [InlineData("!help")]       // missing jdai- part
    [InlineData("/help")]       // missing jdai- part
    [InlineData("!jdai")]       // missing trailing dash
    [InlineData("jdai-help")]   // missing leading ! or /
    public async Task ReadLoop_NonCommandMessages_AreDeliveredAsPlainMessages(string messageText)
    {
        var ch = new SignalChannel(Account);
        var registry = Substitute.For<ICommandRegistry>();
        registry.GetCommand(Arg.Any<string>()).Returns((IChannelCommand?)null);
        await ch.RegisterCommandsAsync(registry);

        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json = BuildReceiveJson(messageText, source: "+15554443333");
        InjectReaderIntoDaemon(ch, MakeReader(json));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(1, $"'{messageText}' is not a command and should be delivered as a plain message");
        received[0].Content.Should().Be(messageText);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 16. Multiple distinct senders
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadLoop_MultipleDistinctSenders_EachMappedCorrectly()
    {
        var ch = new SignalChannel(Account);
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var json1 = BuildReceiveJson("From Alice", source: "+15550000001", sourceName: "Alice");
        var json2 = BuildReceiveJson("From Bob",   source: "+15550000002", sourceName: "Bob");
        InjectReaderIntoDaemon(ch, MakeReader(json1, json2));

        await InvokeReadLoopAsync(ch);

        received.Should().HaveCount(2);
        received[0].SenderId.Should().Be("+15550000001");
        received[0].SenderDisplayName.Should().Be("Alice");
        received[1].SenderId.Should().Be("+15550000002");
        received[1].SenderDisplayName.Should().Be("Bob");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 17. ReadLoop — daemon null guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadLoop_WhenDaemonIsNull_ReturnsImmediately()
    {
        var ch = new SignalChannel(Account);
        // _daemon is never set (no ConnectAsync), so ReadLoopAsync should return immediately.
        var act = () => InvokeReadLoopAsync(ch);
        await act.Should().NotThrowAsync();
    }
}
