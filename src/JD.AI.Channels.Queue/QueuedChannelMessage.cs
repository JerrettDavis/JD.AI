using System.Text.Json;
using JD.AI.Core.Channels;

namespace JD.AI.Channels.Queue;

/// <summary>
/// A persisted, retryable wrapper around a <see cref="ChannelMessage"/>.
/// </summary>
public sealed class QueuedChannelMessage
{
    public const int MaxAttempts = 5;

    public long RowId { get; init; }
    public required string MessageId { get; init; }
    public required string ChannelId { get; init; }
    public required string SenderId { get; init; }
    public string? SenderDisplayName { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? ThreadId { get; init; }
    public string? ReplyToMessageId { get; init; }
    public string? SerializedAttachments { get; init; }
    public string? SerializedMetadata { get; init; }
    public int AttemptCount { get; set; }
    public QueueStatus Status { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset NextRetryAfter { get; set; }
    public DateTimeOffset EnqueuedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }

    public ChannelMessage ToChannelMessage()
    {
        IReadOnlyList<ChannelAttachment> attachments = string.IsNullOrEmpty(SerializedAttachments)
            ? []
            : JsonSerializer.Deserialize<List<ChannelAttachment>>(SerializedAttachments) ?? [];

        IReadOnlyDictionary<string, string> metadata = string.IsNullOrEmpty(SerializedMetadata)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(SerializedMetadata) ?? [];

        return new ChannelMessage
        {
            Id = MessageId,
            ChannelId = ChannelId,
            SenderId = SenderId,
            SenderDisplayName = SenderDisplayName,
            Content = Content,
            Timestamp = Timestamp,
            ThreadId = ThreadId,
            ReplyToMessageId = ReplyToMessageId,
            Attachments = attachments,
            Metadata = metadata
        };
    }
}

public enum QueueStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
