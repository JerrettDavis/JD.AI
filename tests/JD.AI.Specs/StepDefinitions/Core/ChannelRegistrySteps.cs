using FluentAssertions;
using JD.AI.Core.Channels;
using NSubstitute;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class ChannelRegistrySteps
{
    private readonly ScenarioContext _context;

    public ChannelRegistrySteps(ScenarioContext context) => _context = context;

    [Given(@"a channel registry")]
    public void GivenAChannelRegistry()
    {
        _context.Set(new ChannelRegistry(), "ChannelRegistry");
    }

    [Given(@"I have registered channels:")]
    public void GivenIHaveRegisteredChannels(Table table)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        foreach (var row in table.Rows)
        {
            var channel = CreateMockChannel(row["channelType"]);
            registry.Register(channel);
        }
    }

    [Given(@"I have registered a channel of type ""(.*)""")]
    public void GivenIHaveRegisteredAChannelOfType(string channelType)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        registry.Register(CreateMockChannel(channelType));
    }

    [When(@"I register a channel of type ""(.*)""")]
    public void WhenIRegisterAChannelOfType(string channelType)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        registry.Register(CreateMockChannel(channelType));
    }

    [When(@"I list all channels")]
    public void WhenIListAllChannels()
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        _context.Set(registry.Channels, "ChannelList");
    }

    [When(@"I get channel ""(.*)""")]
    public void WhenIGetChannel(string channelType)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        var channel = registry.GetChannel(channelType);
        _context.Set(channel, "FoundChannel");
    }

    [When(@"I unregister channel ""(.*)""")]
    public void WhenIUnregisterChannel(string channelType)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        registry.Unregister(channelType);
    }

    [Then(@"the registry should contain (\d+) channels?")]
    public void ThenTheRegistryShouldContainChannels(int count)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        registry.Channels.Should().HaveCount(count);
    }

    [Then(@"the registry should have channel type ""(.*)""")]
    public void ThenTheRegistryShouldHaveChannelType(string channelType)
    {
        var registry = _context.Get<ChannelRegistry>("ChannelRegistry");
        registry.Channels.Should().Contain(c => c.ChannelType == channelType);
    }

    [Then(@"I should see (\d+) channels")]
    public void ThenIShouldSeeChannels(int count)
    {
        var channels = _context.Get<IReadOnlyList<IChannel>>("ChannelList");
        channels.Should().HaveCount(count);
    }

    [Then(@"the returned channel type should be ""(.*)""")]
    public void ThenTheReturnedChannelTypeShouldBe(string expected)
    {
        var channel = _context.Get<IChannel?>("FoundChannel");
        channel.Should().NotBeNull();
        channel!.ChannelType.Should().Be(expected);
    }

    [Then(@"the returned channel should be null")]
    public void ThenTheReturnedChannelShouldBeNull()
    {
        var channel = _context.Get<IChannel?>("FoundChannel");
        channel.Should().BeNull();
    }

    private static IChannel CreateMockChannel(string channelType)
    {
        var channel = Substitute.For<IChannel>();
        channel.ChannelType.Returns(channelType);
        channel.DisplayName.Returns($"Mock {channelType}");
        channel.IsConnected.Returns(true);
        return channel;
    }
}
