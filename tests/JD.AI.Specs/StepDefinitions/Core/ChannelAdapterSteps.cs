using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using JD.AI.Channels.Discord;
using JD.AI.Channels.Signal;
using JD.AI.Channels.Slack;
using JD.AI.Channels.Telegram;
using JD.AI.Channels.Web;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ChannelAdapterSteps
{
    private readonly ScenarioContext _context;

    public ChannelAdapterSteps(ScenarioContext context) => _context = context;

    private IChannel Channel
    {
        get => _context.Get<IChannel>("Channel");
        set => _context.Set(value, "Channel");
    }

    [Given(@"a (.*) channel adapter")]
    public void GivenAChannelAdapter(string channel)
    {
        Channel = CreateChannel(channel);
    }

    [Given(@"a (.*) channel adapter that supports commands")]
    public void GivenAChannelAdapterThatSupportsCommands(string channel)
    {
        var ch = CreateChannel(channel);
        ch.Should().BeAssignableTo<ICommandAwareChannel>(
            $"{channel} should implement ICommandAwareChannel");
        Channel = ch;
    }

    [Given(@"the channel is connected")]
    public async Task GivenTheChannelIsConnected()
    {
        await Channel.ConnectAsync();
    }

    [When(@"I connect the channel")]
    public async Task WhenIConnectTheChannel()
    {
        await Channel.ConnectAsync();
    }

    [When(@"I disconnect the channel")]
    public async Task WhenIDisconnectTheChannel()
    {
        await Channel.DisconnectAsync();
    }

    [When(@"a message ""(.*)"" arrives from user ""(.*)"" on connection ""(.*)""")]
    public async Task WhenAMessageArrivesFromUserOnConnection(
        string content, string userId, string connectionId)
    {
        var web = (WebChannel)Channel;
        ChannelMessage? received = null;
        web.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        await web.IngestMessageAsync(connectionId, userId, content);

        _context.Set(received, "ReceivedMessage");
    }

    [When(@"I send ""(.*)"" to conversation ""(.*)""")]
    public async Task WhenISendToConversation(string content, string conversationId)
    {
        await Channel.SendMessageAsync(conversationId, content);
    }

    [When(@"I register a command registry")]
    public async Task WhenIRegisterACommandRegistry()
    {
        var registry = Substitute.For<ICommandRegistry>();
        registry.Commands.Returns(new List<IChannelCommand>());

        var aware = (ICommandAwareChannel)Channel;
        Exception? caught = null;
        try
        {
            await aware.RegisterCommandsAsync(registry);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        _context.Set(caught, "CaughtException");
    }

    [When(@"I dispose the channel")]
    public async Task WhenIDisposeTheChannel()
    {
        Exception? caught = null;
        try
        {
            await Channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        _context.Set(caught, "CaughtException");
    }

    [Then(@"the channel type should be ""(.*)""")]
    public void ThenTheChannelTypeShouldBe(string expected)
    {
        Channel.ChannelType.Should().Be(expected);
    }

    [Then(@"the display name should be ""(.*)""")]
    public void ThenTheDisplayNameShouldBe(string expected)
    {
        Channel.DisplayName.Should().Be(expected);
    }

    [Then(@"the channel should be connected")]
    public void ThenTheChannelShouldBeConnected()
    {
        Channel.IsConnected.Should().BeTrue();
    }

    [Then(@"the channel should not be connected")]
    public void ThenTheChannelShouldNotBeConnected()
    {
        Channel.IsConnected.Should().BeFalse();
    }

    [Then(@"the message received event should fire")]
    public void ThenTheMessageReceivedEventShouldFire()
    {
        var msg = _context.Get<ChannelMessage?>("ReceivedMessage");
        msg.Should().NotBeNull();
    }

    [Then(@"the message content should be ""(.*)""")]
    public void ThenTheMessageContentShouldBe(string expected)
    {
        var msg = _context.Get<ChannelMessage?>("ReceivedMessage");
        msg!.Content.Should().Be(expected);
    }

    [Then(@"the message sender ID should be ""(.*)""")]
    public void ThenTheMessageSenderIdShouldBe(string expected)
    {
        var msg = _context.Get<ChannelMessage?>("ReceivedMessage");
        msg!.SenderId.Should().Be(expected);
    }

    [Then(@"the conversation ""(.*)"" should have (\d+) stored messages?")]
    public void ThenTheConversationShouldHaveStoredMessages(string conversationId, int count)
    {
        var web = (WebChannel)Channel;
        // Access the private _conversations field via reflection
        var field = typeof(WebChannel).GetField(
            "_conversations", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull("WebChannel should have a _conversations field");

        var conversations =
            (ConcurrentDictionary<string, List<string>>)field!.GetValue(web)!;
        conversations.Should().ContainKey(conversationId);
        conversations[conversationId].Should().HaveCount(count);
    }

    [Then(@"no error should occur")]
    public void ThenNoErrorShouldOccur()
    {
        var caught = _context.ContainsKey("CaughtException")
            ? _context.Get<Exception?>("CaughtException")
            : null;
        caught.Should().BeNull();
    }

    private static IChannel CreateChannel(string channel) => channel.ToUpperInvariant() switch
    {
        "DISCORD" => new DiscordChannel("fake-token"),
        "SLACK" => new SlackChannel("fake-bot-token", "fake-app-token"),
        "TELEGRAM" => new TelegramChannel("fake-token"),
        "SIGNAL" => new SignalChannel("test-acc"),
        "WEB" => new WebChannel(),
        _ => throw new ArgumentException($"Unknown channel type: {channel}", nameof(channel))
    };
}
