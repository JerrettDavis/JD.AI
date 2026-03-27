namespace JD.AI.Core.Channels;

/// <summary>
/// Represents a message from any channel (Discord, Signal, Slack, Web, etc.).
/// </summary>
public record ChannelMessage
{
    public required string Id { get; init; }
    public required string ChannelId { get; init; }
    public required string SenderId { get; init; }
    public string? SenderDisplayName { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? ThreadId { get; init; }
    public string? ReplyToMessageId { get; init; }
    public IReadOnlyList<ChannelAttachment> Attachments { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>File or image attachment on an inbound channel message.</summary>
public record ChannelAttachment(
    string FileName,
    string ContentType,
    long SizeBytes,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);

/// <summary>File attachment for outbound messages.</summary>
public record OutboundAttachment(
    string FileName,
    Stream Content,
    string? Description = null);

/// <summary>
/// Unified abstraction for a messaging channel (Discord, Slack, Signal, Web, etc.).
/// </summary>
public interface IChannel : IAsyncDisposable
{
    /// <summary>Unique channel type identifier (e.g., "discord", "signal", "web").</summary>
    string ChannelType { get; }

    /// <summary>Display name for this channel instance.</summary>
    string DisplayName { get; }

    /// <summary>Whether the channel is currently connected and healthy.</summary>
    bool IsConnected { get; }

    /// <summary>Connects to the messaging platform.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Gracefully disconnects.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Sends a message to the specified conversation/thread.</summary>
    Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with file attachments. Channels that don't support attachments
    /// fall back to sending the text content only.
    /// </summary>
    Task SendMessageWithAttachmentsAsync(
        string conversationId,
        string? content,
        IReadOnlyList<OutboundAttachment> attachments,
        CancellationToken ct = default)
        => SendMessageAsync(conversationId, content ?? string.Empty, ct);

    /// <summary>
    /// Adds a reaction emoji to a message. Not all channels support this — default is no-op.
    /// </summary>
    Task ReactAsync(string conversationId, string messageId, string emoji, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Raised when a new inbound message arrives.</summary>
    /// <remarks>Uses <c>Func&lt;ChannelMessage, Task&gt;</c> instead of <c>EventHandler</c> to support async handlers.</remarks>
#pragma warning disable CA1003 // Async event pattern requires Func<T, Task> delegate
    event Func<ChannelMessage, Task>? MessageReceived;
#pragma warning restore CA1003
}

/// <summary>
/// Registry that manages all active channel adapters.
/// </summary>
public interface IChannelRegistry
{
    IReadOnlyList<IChannel> Channels { get; }
    void Register(IChannel channel);
    void Unregister(string channelType);
    IChannel? GetChannel(string channelType);
}

/// <summary>
/// Default in-memory channel registry.
/// </summary>
public sealed class ChannelRegistry : IChannelRegistry
{
    private readonly List<IChannel> _channels = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<IChannel> Channels
    {
        get { lock (_lock) return _channels.ToList().AsReadOnly(); }
    }

    public void Register(IChannel channel)
    {
        lock (_lock) _channels.Add(channel);
    }

    public void Unregister(string channelType)
    {
        lock (_lock) _channels.RemoveAll(c => string.Equals(c.ChannelType, channelType, StringComparison.Ordinal));
    }

    public IChannel? GetChannel(string channelType)
    {
        lock (_lock) return _channels.FirstOrDefault(c => string.Equals(c.ChannelType, channelType, StringComparison.Ordinal));
    }
}
