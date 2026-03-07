using System.Collections.Concurrent;
using JD.AI.Core.Channels;

namespace JD.AI.Channels.Web;

/// <summary>
/// WebChat channel adapter for browser-based conversations via SignalR.
/// This is a lightweight adapter that acts as a bridge between the
/// Gateway's SignalR hub and the IChannel abstraction.
/// </summary>
public sealed class WebChannel : IChannel
{
    private readonly ConcurrentDictionary<string, List<string>> _conversations = new();
    private bool _connected;

    public string ChannelType => "web";
    public string DisplayName => "WebChat";
    public bool IsConnected => _connected;

    public event Func<ChannelMessage, Task>? MessageReceived;

    public Task ConnectAsync(CancellationToken ct = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _connected = false;
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        // WebChat responses are sent back through SignalR hub directly.
        // This method is a no-op here; the hub handles outbound messages.
        _conversations.GetOrAdd(conversationId, _ => []).Add(content);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by the SignalR hub when a browser client sends a message.
    /// </summary>
    public async Task IngestMessageAsync(string connectionId, string userId, string content)
    {
        var msg = new ChannelMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            ChannelId = connectionId,
            SenderId = userId,
            SenderDisplayName = userId,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow
        };

        if (MessageReceived is not null)
            await MessageReceived.Invoke(msg);
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }
}
