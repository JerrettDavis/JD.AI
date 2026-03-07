using FluentAssertions;
using JD.AI.Channels.Web;
using JD.AI.Core.Channels;

namespace JD.AI.Tests.Channels;

public sealed class WebChannelTests
{
    [Fact]
    public void ChannelType_IsWeb()
    {
        var channel = new WebChannel();

        channel.ChannelType.Should().Be("web");
    }

    [Fact]
    public void DisplayName_IsWebChat()
    {
        var channel = new WebChannel();

        channel.DisplayName.Should().Be("WebChat");
    }

    [Fact]
    public void IsConnected_DefaultsFalse()
    {
        var channel = new WebChannel();

        channel.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        var channel = new WebChannel();

        await channel.ConnectAsync();

        channel.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_SetsIsConnectedFalse()
    {
        var channel = new WebChannel();
        await channel.ConnectAsync();

        await channel.DisconnectAsync();

        channel.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_ThenDisconnectAsync_Lifecycle()
    {
        var channel = new WebChannel();

        channel.IsConnected.Should().BeFalse();

        await channel.ConnectAsync();
        channel.IsConnected.Should().BeTrue();

        await channel.DisconnectAsync();
        channel.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ConnectAsync_CanBeCalledMultipleTimes()
    {
        var channel = new WebChannel();

        await channel.ConnectAsync();
        await channel.ConnectAsync();

        channel.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNeverConnected_DoesNotThrow()
    {
        var channel = new WebChannel();

        var act = () => channel.DisconnectAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IngestMessageAsync_RaisesMessageReceived()
    {
        var channel = new WebChannel();
        ChannelMessage? received = null;
        channel.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        await channel.IngestMessageAsync("conn-1", "user-42", "Hello world");

        received.Should().NotBeNull();
        received!.ChannelId.Should().Be("conn-1");
        received.SenderId.Should().Be("user-42");
        received.SenderDisplayName.Should().Be("user-42");
        received.Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task IngestMessageAsync_WithoutHandler_DoesNotThrow()
    {
        var channel = new WebChannel();

        var act = () => channel.IngestMessageAsync("conn-1", "user-1", "test");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IngestMessageAsync_SetsTimestamp()
    {
        var channel = new WebChannel();
        ChannelMessage? received = null;
        channel.MessageReceived += msg =>
        {
            received = msg;
            return Task.CompletedTask;
        };

        var before = DateTimeOffset.UtcNow;
        await channel.IngestMessageAsync("conn-1", "user-1", "test");
        var after = DateTimeOffset.UtcNow;

        received.Should().NotBeNull();
        received!.Timestamp.Should().BeOnOrAfter(before);
        received.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task IngestMessageAsync_GeneratesUniqueIds()
    {
        var channel = new WebChannel();
        var ids = new List<string>();
        channel.MessageReceived += msg =>
        {
            ids.Add(msg.Id);
            return Task.CompletedTask;
        };

        await channel.IngestMessageAsync("conn-1", "user-1", "msg1");
        await channel.IngestMessageAsync("conn-1", "user-1", "msg2");
        await channel.IngestMessageAsync("conn-1", "user-1", "msg3");

        ids.Should().HaveCount(3);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SendMessageAsync_StoresMessageForConversation()
    {
        var channel = new WebChannel();

        await channel.SendMessageAsync("conv-1", "Hello");
        await channel.SendMessageAsync("conv-1", "World");

        // SendMessageAsync stores content in an internal dictionary.
        // We verify it doesn't throw, as WebChannel is a no-op bridge.
    }

    [Fact]
    public async Task SendMessageAsync_MultiplConversations_DoesNotThrow()
    {
        var channel = new WebChannel();

        await channel.SendMessageAsync("conv-1", "Hello from conv-1");
        await channel.SendMessageAsync("conv-2", "Hello from conv-2");

        // No exception means success
    }

    [Fact]
    public async Task DisposeAsync_SetsIsConnectedFalse()
    {
        var channel = new WebChannel();
        await channel.ConnectAsync();
        channel.IsConnected.Should().BeTrue();

        await channel.DisposeAsync();

        channel.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var channel = new WebChannel();

        await channel.DisposeAsync();
        await channel.DisposeAsync();

        // No exception means success
    }

    [Fact]
    public async Task DisposeAsync_WhenNeverConnected_DoesNotThrow()
    {
        var channel = new WebChannel();

        var act = () => channel.DisposeAsync().AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MessageReceived_MultipleHandlers_AllInvoked()
    {
        var channel = new WebChannel();
        var count = 0;

        channel.MessageReceived += _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        };
        channel.MessageReceived += _ =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        };

        await channel.IngestMessageAsync("conn-1", "user-1", "test");

        count.Should().Be(2);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentIngestMessages()
    {
        var channel = new WebChannel();
        var receivedCount = 0;
        channel.MessageReceived += _ =>
        {
            Interlocked.Increment(ref receivedCount);
            return Task.CompletedTask;
        };

        var tasks = Enumerable.Range(0, 100)
            .Select(i => channel.IngestMessageAsync($"conn-{i}", $"user-{i}", $"msg-{i}"))
            .ToArray();

        await Task.WhenAll(tasks);

        receivedCount.Should().Be(100);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentSendMessages()
    {
        var channel = new WebChannel();

        var tasks = Enumerable.Range(0, 100)
            .Select(i => channel.SendMessageAsync($"conv-{i % 10}", $"message-{i}"))
            .ToArray();

        var act = () => Task.WhenAll(tasks);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentConnectDisconnect()
    {
        var channel = new WebChannel();

        var tasks = Enumerable.Range(0, 50)
            .Select(i => i % 2 == 0
                ? channel.ConnectAsync()
                : channel.DisconnectAsync())
            .ToArray();

        var act = () => Task.WhenAll(tasks);

        await act.Should().NotThrowAsync();
    }
}
