using FluentAssertions;
using JD.AI.Core.Channels;

namespace JD.AI.Tests.Channels;

/// <summary>
/// Tests for <see cref="ChannelRegistry"/> in-memory channel management.
/// </summary>
public sealed class ChannelRegistryTests
{
    private sealed class FakeChannel : IChannel
    {
        public FakeChannel(string channelType, string displayName = "Fake")
        {
            ChannelType = channelType;
            DisplayName = displayName;
        }

        public string ChannelType { get; }
        public string DisplayName { get; }
        public bool IsConnected => false;

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

#pragma warning disable CS0067 // event never used in tests
        public event Func<ChannelMessage, Task>? MessageReceived;
#pragma warning restore CS0067
    }

    [Fact]
    public void Channels_InitiallyEmpty()
    {
        var registry = new ChannelRegistry();
        registry.Channels.Should().BeEmpty();
    }

    [Fact]
    public void Register_AddsChannel()
    {
        var registry = new ChannelRegistry();
        var channel = new FakeChannel("discord");
        registry.Register(channel);

        registry.Channels.Should().HaveCount(1);
        registry.Channels[0].ChannelType.Should().Be("discord");
    }

    [Fact]
    public void Register_MultipleChannels()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord"));
        registry.Register(new FakeChannel("slack"));
        registry.Register(new FakeChannel("signal"));

        registry.Channels.Should().HaveCount(3);
    }

    [Fact]
    public void GetChannel_ExistingType_ReturnsChannel()
    {
        var registry = new ChannelRegistry();
        var channel = new FakeChannel("slack");
        registry.Register(channel);

        registry.GetChannel("slack").Should().BeSameAs(channel);
    }

    [Fact]
    public void GetChannel_NonexistentType_ReturnsNull()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord"));

        registry.GetChannel("slack").Should().BeNull();
    }

    [Fact]
    public void GetChannel_CaseSensitive()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord"));

        // ChannelType comparison is Ordinal (case-sensitive)
        registry.GetChannel("Discord").Should().BeNull();
    }

    [Fact]
    public void Unregister_RemovesChannel()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord"));
        registry.Register(new FakeChannel("slack"));

        registry.Unregister("discord");

        registry.Channels.Should().HaveCount(1);
        registry.GetChannel("discord").Should().BeNull();
        registry.GetChannel("slack").Should().NotBeNull();
    }

    [Fact]
    public void Unregister_NonexistentType_DoesNotThrow()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord"));

        var act = () => registry.Unregister("nonexistent");
        act.Should().NotThrow();
        registry.Channels.Should().HaveCount(1);
    }

    [Fact]
    public void Unregister_RemovesAllMatchingType()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", "Server1"));
        registry.Register(new FakeChannel("discord", "Server2"));
        registry.Register(new FakeChannel("slack"));

        registry.Unregister("discord");

        registry.Channels.Should().HaveCount(1);
        registry.Channels[0].ChannelType.Should().Be("slack");
    }

    [Fact]
    public void Channels_ReturnsSnapshot()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord"));

        var snapshot = registry.Channels;
        registry.Register(new FakeChannel("slack"));

        // Snapshot should not reflect subsequent changes
        snapshot.Should().HaveCount(1);
        registry.Channels.Should().HaveCount(2);
    }
}
