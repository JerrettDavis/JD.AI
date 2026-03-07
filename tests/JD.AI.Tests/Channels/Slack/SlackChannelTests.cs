using FluentAssertions;
using JD.AI.Channels.Slack;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SlackNet.Events;

namespace JD.AI.Tests.Channels.Slack;

/// <summary>
/// Comprehensive tests for <see cref="SlackChannel"/>.
/// Live socket-mode connections and real Slack API calls are not tested here.
/// All testable logic — construction, state, message handling, slash-command
/// dispatch, event mapping, and disposal — is covered.
/// </summary>
public sealed class SlackChannelTests
{
    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithTokens_DoesNotThrow()
    {
        var act = () => new SlackChannel("xoxb-bot-token", "xapp-app-token");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEmptyTokens_DoesNotThrow()
    {
        // SlackChannel accepts any string; token validation happens at connection time.
        var act = () => new SlackChannel(string.Empty, string.Empty);
        act.Should().NotThrow();
    }

    // -------------------------------------------------------------------------
    // Static constants / properties
    // -------------------------------------------------------------------------

    [Fact]
    public void CommandPrefix_IsSlashJdaiDash()
    {
        SlackChannel.CommandPrefix.Should().Be("/jdai-");
    }

    [Fact]
    public void CommandPrefix_StartsWithSlash()
    {
        SlackChannel.CommandPrefix.Should().StartWith("/");
    }

    [Fact]
    public void ChannelType_IsSlack()
    {
        var ch = new SlackChannel("bot", "app");
        ch.ChannelType.Should().Be("slack");
    }

    [Fact]
    public void DisplayName_IsSlack()
    {
        var ch = new SlackChannel("bot", "app");
        ch.DisplayName.Should().Be("Slack");
    }

    // -------------------------------------------------------------------------
    // Interface conformance
    // -------------------------------------------------------------------------

    [Fact]
    public void ImplementsIChannel()
    {
        new SlackChannel("bot", "app").Should().BeAssignableTo<IChannel>();
    }

    [Fact]
    public void ImplementsICommandAwareChannel()
    {
        new SlackChannel("bot", "app").Should().BeAssignableTo<ICommandAwareChannel>();
    }

    [Fact]
    public void ImplementsIAsyncDisposable()
    {
        new SlackChannel("bot", "app").Should().BeAssignableTo<IAsyncDisposable>();
    }

    // -------------------------------------------------------------------------
    // IsConnected state machine
    // -------------------------------------------------------------------------

    [Fact]
    public void IsConnected_BeforeConnect_IsFalse()
    {
        var ch = new SlackChannel("bot", "app");
        ch.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.ConnectAsync();
        ch.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_AfterConnect_SetsIsConnectedFalse()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.ConnectAsync();
        await ch.DisconnectAsync();
        ch.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new SlackChannel("bot", "app");
        var act = async () => await ch.DisconnectAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_IsConnectedRemainsFlase()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.DisconnectAsync();
        ch.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_ThenDisconnect_ThenConnect_SetsConnectedAgain()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.ConnectAsync();
        await ch.DisconnectAsync();
        await ch.ConnectAsync();
        ch.IsConnected.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // MessageReceived event
    // -------------------------------------------------------------------------

    [Fact]
    public void MessageReceived_CanSubscribeAndUnsubscribe()
    {
        var ch = new SlackChannel("bot", "app");
        Func<ChannelMessage, Task> handler = static _ => Task.CompletedTask;

        var attachAct = () => { ch.MessageReceived += handler; };
        var detachAct = () => { ch.MessageReceived -= handler; };

        attachAct.Should().NotThrow();
        detachAct.Should().NotThrow();
    }

    [Fact]
    public async Task MessageReceived_InitiallyNull_NoHandlersSubscribed()
    {
        // The event is a field-backed Func delegate; initially null means no subscribers.
        // We verify this indirectly: HandleMessageAsync with no subscribers does not throw.
        var ch = new SlackChannel("bot", "app");
        var slackEvent = BuildMessageEvent("U123", "C456", "hello", "111.000");

        var act = async () => await ch.HandleMessageAsync(slackEvent);
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // HandleMessageAsync — message mapping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleMessageAsync_WithUser_RaisesMessageReceivedOnce()
    {
        var ch = new SlackChannel("bot", "app");
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        };

        var slackEvent = BuildMessageEvent("UABC", "CABC", "hello world", "1700000000.000001");
        await ch.HandleMessageAsync(slackEvent);

        received.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleMessageAsync_MapsUserToSenderId()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U_USER1", "C_CHAN1", "hi", "100.001");
        await ch.HandleMessageAsync(slackEvent);

        captured.Should().NotBeNull();
        captured!.SenderId.Should().Be("U_USER1");
    }

    [Fact]
    public async Task HandleMessageAsync_MapsChannelToChannelId()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C_GENERAL", "hi", "100.001");
        await ch.HandleMessageAsync(slackEvent);

        captured!.ChannelId.Should().Be("C_GENERAL");
    }

    [Fact]
    public async Task HandleMessageAsync_MapsTextToContent()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C1", "the message text", "1.0");
        await ch.HandleMessageAsync(slackEvent);

        captured!.Content.Should().Be("the message text");
    }

    [Fact]
    public async Task HandleMessageAsync_MapsTsToId()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C1", "msg", "1700000000.000001");
        await ch.HandleMessageAsync(slackEvent);

        captured!.Id.Should().Be("1700000000.000001");
    }

    [Fact]
    public async Task HandleMessageAsync_WhenTsIsNull_GeneratesGuidId()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C1", "msg", ts: null);
        await ch.HandleMessageAsync(slackEvent);

        // When Ts is null the code falls back to Guid.NewGuid().ToString("N")
        captured!.Id.Should().NotBeNullOrEmpty();
        // A "N"-formatted GUID is 32 hex chars, no hyphens
        captured.Id.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task HandleMessageAsync_WhenChannelIsNull_UsesUnknown()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", channel: null, "msg", "1.0");
        await ch.HandleMessageAsync(slackEvent);

        captured!.ChannelId.Should().Be("unknown");
    }

    [Fact]
    public async Task HandleMessageAsync_WhenTextIsNull_UsesEmptyString()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C1", text: null, "1.0");
        await ch.HandleMessageAsync(slackEvent);

        captured!.Content.Should().Be(string.Empty);
    }

    [Fact]
    public async Task HandleMessageAsync_WithThreadTs_MapsToThreadId()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C1", "reply", "2.0", threadTs: "1.0");
        await ch.HandleMessageAsync(slackEvent);

        captured!.ThreadId.Should().Be("1.0");
    }

    [Fact]
    public async Task HandleMessageAsync_WithoutThreadTs_ThreadIdIsNull()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var slackEvent = BuildMessageEvent("U1", "C1", "top-level", "1.0", threadTs: null);
        await ch.HandleMessageAsync(slackEvent);

        captured!.ThreadId.Should().BeNull();
    }

    [Fact]
    public async Task HandleMessageAsync_TimestampIsApproximatelyNow()
    {
        var ch = new SlackChannel("bot", "app");
        ChannelMessage? captured = null;
        ch.MessageReceived += msg => { captured = msg; return Task.CompletedTask; };

        var before = DateTimeOffset.UtcNow;
        var slackEvent = BuildMessageEvent("U1", "C1", "hi", "1.0");
        await ch.HandleMessageAsync(slackEvent);
        var after = DateTimeOffset.UtcNow;

        captured!.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // -------------------------------------------------------------------------
    // HandleMessageAsync — bot-message filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleMessageAsync_WhenUserIsNull_SkipsBotMessage()
    {
        // Bot messages have User == null; they should be silently dropped.
        var ch = new SlackChannel("bot", "app");
        var received = new List<ChannelMessage>();
        ch.MessageReceived += msg => { received.Add(msg); return Task.CompletedTask; };

        var botEvent = new MessageEvent { User = null, Channel = "C1", Text = "bot says" };
        await ch.HandleMessageAsync(botEvent);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleMessageAsync_WhenUserIsNull_DoesNotThrow()
    {
        var ch = new SlackChannel("bot", "app");
        var botEvent = new MessageEvent { User = null };
        var act = async () => await ch.HandleMessageAsync(botEvent);
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // HandleMessageAsync — multiple subscribers
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleMessageAsync_WithMultipleSubscribers_RaisesAll()
    {
        var ch = new SlackChannel("bot", "app");
        var count = 0;
        ch.MessageReceived += _ => { count++; return Task.CompletedTask; };
        ch.MessageReceived += _ => { count++; return Task.CompletedTask; };

        await ch.HandleMessageAsync(BuildMessageEvent("U1", "C1", "hi", "1.0"));

        count.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // SendMessageAsync — not connected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendMessageAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ch = new SlackChannel("bot", "app");
        var act = async () => await ch.SendMessageAsync("C123", "hello");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not connected*");
    }

    // -------------------------------------------------------------------------
    // RegisterCommandsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegisterCommandsAsync_ReturnsCompletedTask()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var task = ch.RegisterCommandsAsync(registry);
        await task;
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterCommandsAsync_DoesNotThrow()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var act = async () => await ch.RegisterCommandsAsync(registry);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterCommandsAsync_AcceptsNullRegistry()
    {
        // ICommandRegistry is an interface; passing null is unusual but should not crash
        // the registration call itself (the registry is stored and used later).
        var ch = new SlackChannel("bot", "app");
        var act = async () => await ch.RegisterCommandsAsync(null!);
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — no registry
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_WithoutRegistry_ReturnsCommandsNotAvailable()
    {
        var ch = new SlackChannel("bot", "app");
        var result = await ch.HandleSlashCommandAsync("jdai-help", "", "U1", "C1");
        result.Should().Be("Commands not available.");
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — unknown command
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_UnknownCommand_ReturnsUnknownMessage()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        registry.GetCommand(Arg.Any<string>()).Returns((IChannelCommand?)null);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("jdai-unknown", "", "U1", "C1");

        result.Should().Contain("Unknown command");
        result.Should().Contain("jdai-unknown");
        result.Should().Contain("/jdai-help");
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — prefix stripping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_StripsJdaiPrefix_BeforeLookup()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = BuildCommand("help", [], new CommandResult { Success = true, Content = "Help text" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("jdai-help", "", "U1", "C1");

        result.Should().Be("Help text");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_WithoutJdaiPrefix_LooksUpNameDirectly()
    {
        // If the command name does NOT start with "jdai-", it is used as-is.
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = BuildCommand("help", [], new CommandResult { Success = true, Content = "Help!" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("help", "", "U1", "C1");

        result.Should().Be("Help!");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_PrefixStrip_IsCaseInsensitive()
    {
        // "JDAI-help" should be stripped the same as "jdai-help".
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = BuildCommand("help", [], new CommandResult { Success = true, Content = "OK" });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("JDAI-help", "", "U1", "C1");

        result.Should().Be("OK");
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — argument parsing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_ParsesTextIntoPositionalArgs()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "use",
            [new CommandParameter { Name = "model", Description = "Model" }],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "done" }; });
        registry.GetCommand("use").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-use", "claude-3", "U1", "C1");

        capturedContext.Should().NotBeNull();
        capturedContext!.Arguments.Should().ContainKey("model").WhoseValue.Should().Be("claude-3");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_IgnoresExtraTextBeyondParameterCount()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "use",
            [new CommandParameter { Name = "model", Description = "Model" }],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "done" }; });
        registry.GetCommand("use").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-use", "claude-3 extra ignored", "U1", "C1");

        capturedContext!.Arguments.Should().HaveCount(1);
        capturedContext.Arguments["model"].Should().Be("claude-3");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_WithEmptyText_PassesEmptyArgs()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "help",
            [new CommandParameter { Name = "topic", Description = "Topic", IsRequired = false }],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "Help" }; });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-help", "", "U1", "C1");

        capturedContext!.Arguments.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleSlashCommandAsync_WithWhitespaceOnlyText_PassesEmptyArgs()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "help",
            [],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "Help" }; });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-help", "   ", "U1", "C1");

        capturedContext!.Arguments.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleSlashCommandAsync_ParsesMultiplePositionalArgs()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "set",
            [
                new CommandParameter { Name = "key", Description = "Key" },
                new CommandParameter { Name = "value", Description = "Value" }
            ],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "OK" }; });
        registry.GetCommand("set").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-set", "temperature 0.8", "U1", "C1");

        capturedContext!.Arguments["key"].Should().Be("temperature");
        capturedContext.Arguments["value"].Should().Be("0.8");
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — CommandContext population
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_SetsCommandNameWithoutPrefix()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "help",
            [],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "OK" }; });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-help", "", "UINVOKER", "CCHAN");

        capturedContext!.CommandName.Should().Be("help");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_SetsInvokerIdFromUserId()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "help",
            [],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "OK" }; });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-help", "", "UINVOKER", "CCHAN");

        capturedContext!.InvokerId.Should().Be("UINVOKER");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_SetsChannelIdFromChannelId()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "help",
            [],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "OK" }; });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-help", "", "UINVOKER", "CCHAN");

        capturedContext!.ChannelId.Should().Be("CCHAN");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_SetsChannelTypeToSlack()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        CommandContext? capturedContext = null;
        var command = BuildCommandWithCapture(
            "help",
            [],
            ctx => { capturedContext = ctx; return new CommandResult { Success = true, Content = "OK" }; });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        await ch.HandleSlashCommandAsync("jdai-help", "", "U1", "C1");

        capturedContext!.ChannelType.Should().Be("slack");
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — command result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_ReturnsCommandResultContent()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = BuildCommand(
            "help",
            [],
            new CommandResult { Success = true, Content = "Here is the help text." });
        registry.GetCommand("help").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("jdai-help", "", "U1", "C1");

        result.Should().Be("Here is the help text.");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_WhenCommandFails_ReturnsErrorContent()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = BuildCommand(
            "broken",
            [],
            new CommandResult { Success = false, Content = "Something went wrong." });
        registry.GetCommand("broken").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("jdai-broken", "", "U1", "C1");

        result.Should().Be("Something went wrong.");
    }

    // -------------------------------------------------------------------------
    // HandleSlashCommandAsync — exception handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleSlashCommandAsync_WhenCommandThrows_ReturnsCommandErrorMessage()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("kaboom");
        command.Parameters.Returns(Array.Empty<CommandParameter>() as IReadOnlyList<CommandParameter>);
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("exploded"));
        registry.GetCommand("kaboom").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var result = await ch.HandleSlashCommandAsync("jdai-kaboom", "", "U1", "C1");

        result.Should().StartWith("Command error:");
        result.Should().Contain("exploded");
    }

    [Fact]
    public async Task HandleSlashCommandAsync_WhenCommandThrowsArbitraryException_DoesNotRethrow()
    {
        var ch = new SlackChannel("bot", "app");
        var registry = Substitute.For<ICommandRegistry>();
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns("fail");
        command.Parameters.Returns(Array.Empty<CommandParameter>() as IReadOnlyList<CommandParameter>);
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("bad arg", paramName: null));
        registry.GetCommand("fail").Returns(command);
        await ch.RegisterCommandsAsync(registry);

        var act = async () => await ch.HandleSlashCommandAsync("jdai-fail", "", "U1", "C1");
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // DisposeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_WhenNotConnected_DoesNotThrow()
    {
        var ch = new SlackChannel("bot", "app");
        await using (ch)
        {
            // nothing to do, just verify dispose doesn't throw
        }
    }

    [Fact]
    public async Task DisposeAsync_AfterConnect_SetsIsConnectedFalse()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.ConnectAsync();
        await ch.DisposeAsync();
        ch.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var ch = new SlackChannel("bot", "app");
        await ch.DisposeAsync();
        var act = async () => await ch.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // Helper methods
    // -------------------------------------------------------------------------

    private static MessageEvent BuildMessageEvent(
        string? user,
        string? channel,
        string? text,
        string? ts,
        string? threadTs = null)
    {
        return new MessageEvent
        {
            User = user,
            Channel = channel,
            Text = text,
            Ts = ts,
            ThreadTs = threadTs
        };
    }

    private static IChannelCommand BuildCommand(
        string name,
        IReadOnlyList<CommandParameter> parameters,
        CommandResult result)
    {
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns(name);
        command.Parameters.Returns(parameters);
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        return command;
    }

    private static IChannelCommand BuildCommandWithCapture(
        string name,
        IReadOnlyList<CommandParameter> parameters,
        Func<CommandContext, CommandResult> onExecute)
    {
        var command = Substitute.For<IChannelCommand>();
        command.Name.Returns(name);
        command.Parameters.Returns(parameters);
        command.ExecuteAsync(Arg.Any<CommandContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ctx = call.ArgAt<CommandContext>(0);
                return Task.FromResult(onExecute(ctx));
            });
        return command;
    }
}
