using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Tests for <see cref="ChannelOpsTools"/> channel listing, status, send,
/// connect, and disconnect operations.
/// </summary>
public sealed class ChannelOpsToolsTests
{
    private sealed class FakeChannel : IChannel
    {
        public FakeChannel(string channelType, string displayName = "Fake", bool isConnected = false)
        {
            ChannelType = channelType;
            DisplayName = displayName;
            IsConnected = isConnected;
        }

        public string ChannelType { get; }
        public string DisplayName { get; }
        public bool IsConnected { get; private set; }

        public bool ConnectCalled { get; private set; }
        public bool DisconnectCalled { get; private set; }
        public string? LastSentConversationId { get; private set; }
        public string? LastSentContent { get; private set; }
        public bool ThrowOnConnect { get; set; }
        public bool ThrowOnSend { get; set; }
        public bool ThrowOnDisconnect { get; set; }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            if (ThrowOnConnect) throw new InvalidOperationException("Connection failed");
            ConnectCalled = true;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            if (ThrowOnDisconnect) throw new InvalidOperationException("Disconnect failed");
            DisconnectCalled = true;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
        {
            if (ThrowOnSend) throw new InvalidOperationException("Send failed");
            LastSentConversationId = conversationId;
            LastSentContent = content;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

#pragma warning disable CS0067
        public event Func<ChannelMessage, Task>? MessageReceived;
#pragma warning restore CS0067
    }

    // ── ListChannels ──────────────────────────────────────────────────

    [Fact]
    public void ListChannels_Empty_ReturnsNoChannelsMessage()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);
        var result = tools.ListChannels();
        result.Should().Contain("No channels registered");
    }

    [Fact]
    public void ListChannels_WithChannels_ShowsTable()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", "Bot1", isConnected: true));
        registry.Register(new FakeChannel("slack", "Workspace", isConnected: false));
        var tools = new ChannelOpsTools(registry);

        var result = tools.ListChannels();
        result.Should().Contain("Channels (2)");
        result.Should().Contain("discord");
        result.Should().Contain("Bot1");
        result.Should().Contain("Connected");
        result.Should().Contain("slack");
        result.Should().Contain("Disconnected");
    }

    // ── GetChannelStatus ──────────────────────────────────────────────

    [Fact]
    public void GetChannelStatus_ExistingChannel_ShowsDetails()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", "MyBot", isConnected: true));
        var tools = new ChannelOpsTools(registry);

        var result = tools.GetChannelStatus("discord");
        result.Should().Contain("MyBot");
        result.Should().Contain("discord");
        result.Should().Contain("Yes");
        result.Should().Contain("FakeChannel");
    }

    [Fact]
    public void GetChannelStatus_NonexistentChannel_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = tools.GetChannelStatus("missing");
        result.Should().Contain("not found");
    }

    [Fact]
    public void GetChannelStatus_DisconnectedChannel_ShowsNo()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("slack", "Team", isConnected: false));
        var tools = new ChannelOpsTools(registry);

        var result = tools.GetChannelStatus("slack");
        result.Should().Contain("No");
    }

    // ── SendMessageAsync ──────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_EmptyMessage_ReturnsError()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", isConnected: true));
        var tools = new ChannelOpsTools(registry);

        var result = await tools.SendMessageAsync("discord", "conv-1", "");
        result.Should().Contain("empty");
    }

    [Fact]
    public async Task SendMessage_NonexistentChannel_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = await tools.SendMessageAsync("missing", "conv-1", "Hello");
        result.Should().Contain("not found");
    }

    [Fact]
    public async Task SendMessage_DisconnectedChannel_ReturnsError()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", isConnected: false));
        var tools = new ChannelOpsTools(registry);

        var result = await tools.SendMessageAsync("discord", "conv-1", "Hello");
        result.Should().Contain("not connected");
    }

    [Fact]
    public async Task SendMessage_ConnectedChannel_SendsSuccessfully()
    {
        var channel = new FakeChannel("discord", isConnected: true);
        var registry = new ChannelRegistry();
        registry.Register(channel);
        var tools = new ChannelOpsTools(registry);

        var result = await tools.SendMessageAsync("discord", "conv-1", "Hello world");
        result.Should().Contain("Message sent");
        result.Should().Contain("11 chars");
        channel.LastSentConversationId.Should().Be("conv-1");
        channel.LastSentContent.Should().Be("Hello world");
    }

    [Fact]
    public async Task SendMessage_ChannelThrows_ReturnsError()
    {
        var channel = new FakeChannel("discord", isConnected: true) { ThrowOnSend = true };
        var registry = new ChannelRegistry();
        registry.Register(channel);
        var tools = new ChannelOpsTools(registry);

        var result = await tools.SendMessageAsync("discord", "conv-1", "Hello");
        result.Should().Contain("Error");
    }

    // ── ConnectChannelAsync ──────────────────────────────────────────

    [Fact]
    public async Task Connect_NonexistentChannel_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = await tools.ConnectChannelAsync("missing");
        result.Should().Contain("not found");
    }

    [Fact]
    public async Task Connect_AlreadyConnected_ReturnsAlreadyMessage()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", isConnected: true));
        var tools = new ChannelOpsTools(registry);

        var result = await tools.ConnectChannelAsync("discord");
        result.Should().Contain("already connected");
    }

    [Fact]
    public async Task Connect_DisconnectedChannel_Connects()
    {
        var channel = new FakeChannel("discord", isConnected: false);
        var registry = new ChannelRegistry();
        registry.Register(channel);
        var tools = new ChannelOpsTools(registry);

        var result = await tools.ConnectChannelAsync("discord");
        result.Should().Contain("connected successfully");
        channel.ConnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_ChannelThrows_ReturnsError()
    {
        var channel = new FakeChannel("discord", isConnected: false) { ThrowOnConnect = true };
        var registry = new ChannelRegistry();
        registry.Register(channel);
        var tools = new ChannelOpsTools(registry);

        var result = await tools.ConnectChannelAsync("discord");
        result.Should().Contain("Error");
    }

    // ── DisconnectChannelAsync ──────────────────────────────────────

    [Fact]
    public async Task Disconnect_NonexistentChannel_ReturnsError()
    {
        var registry = new ChannelRegistry();
        var tools = new ChannelOpsTools(registry);

        var result = await tools.DisconnectChannelAsync("missing");
        result.Should().Contain("not found");
    }

    [Fact]
    public async Task Disconnect_AlreadyDisconnected_ReturnsAlreadyMessage()
    {
        var registry = new ChannelRegistry();
        registry.Register(new FakeChannel("discord", isConnected: false));
        var tools = new ChannelOpsTools(registry);

        var result = await tools.DisconnectChannelAsync("discord");
        result.Should().Contain("already disconnected");
    }

    [Fact]
    public async Task Disconnect_ConnectedChannel_Disconnects()
    {
        var channel = new FakeChannel("discord", isConnected: true);
        var registry = new ChannelRegistry();
        registry.Register(channel);
        var tools = new ChannelOpsTools(registry);

        var result = await tools.DisconnectChannelAsync("discord");
        result.Should().Contain("disconnected");
        channel.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Disconnect_ChannelThrows_ReturnsError()
    {
        var channel = new FakeChannel("discord", isConnected: true) { ThrowOnDisconnect = true };
        var registry = new ChannelRegistry();
        registry.Register(channel);
        var tools = new ChannelOpsTools(registry);

        var result = await tools.DisconnectChannelAsync("discord");
        result.Should().Contain("Error");
    }
}
