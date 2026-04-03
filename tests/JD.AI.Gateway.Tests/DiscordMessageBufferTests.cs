using System.Globalization;
using JD.AI.Channels.Queue;
using JD.AI.Core.Channels;

namespace JD.AI.Gateway.Tests;

public sealed class DiscordMessageBufferTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), $"jdai-queue-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task EnqueueDequeueComplete_RoundTripsMessageAndUpdatesStats()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();

        var message = CreateMessage();

        var rowId = await buffer.EnqueueAsync(message);
        var queued = await buffer.DequeueAsync();
        var statsAfterDequeue = await buffer.GetStatsAsync();

        Assert.NotNull(queued);
        Assert.Equal(rowId, queued!.RowId);
        Assert.Equal(message.Id, queued.MessageId);
        Assert.Equal(message.ChannelId, queued.ChannelId);
        Assert.Equal(message.SenderId, queued.SenderId);
        Assert.Equal(message.SenderDisplayName, queued.SenderDisplayName);
        Assert.Equal(message.Content, queued.Content);
        Assert.Equal(message.Timestamp, queued.Timestamp);
        Assert.Equal(message.ThreadId, queued.ThreadId);
        Assert.Equal(message.ReplyToMessageId, queued.ReplyToMessageId);
        Assert.Equal(0, queued.AttemptCount);
        Assert.Equal(QueueStatus.Pending, queued.Status);
        Assert.True(statsAfterDequeue.Processing >= 1);

        var roundTripped = queued.ToChannelMessage();
        Assert.Equal(message.Id, roundTripped.Id);
        Assert.Equal(message.Content, roundTripped.Content);
        Assert.Equal("https://example.test/file.txt", roundTripped.Metadata["attachmentUrl"]);
        Assert.Empty(roundTripped.Attachments);

        await buffer.CompleteAsync(rowId);

        var statsAfterComplete = await buffer.GetStatsAsync();
        Assert.Equal(1, statsAfterComplete.Completed);
        Assert.Equal(1, statsAfterComplete.Total);
    }

    [Fact]
    public async Task RetryAndReset_UpdatePendingStateAndTruncateErrors()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();

        var rowId = await buffer.EnqueueAsync(CreateMessage());
        _ = await buffer.DequeueAsync();

        var longError = new string('x', 2505);
        await buffer.RetryAsync(rowId, longError);

        var pending = await buffer.GetPendingMessagesAsync();
        var retried = Assert.Single(pending);
        Assert.Equal(QueueStatus.Pending, retried.Status);
        Assert.NotNull(retried.LastError);
        Assert.Equal(2000, retried.LastError!.Length);
        Assert.True(retried.NextRetryAfter > DateTimeOffset.UtcNow);

        await buffer.ResetAsync(rowId);

        pending = await buffer.GetPendingMessagesAsync();
        var reset = Assert.Single(pending);
        Assert.Equal(QueueStatus.Pending, reset.Status);
        Assert.Null(reset.LastError);
        Assert.Equal(default, reset.NextRetryAfter);
    }

    [Fact]
    public async Task FailAndPurge_RemovesOldFailedMessagesWhenIncluded()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();

        var rowId = await buffer.EnqueueAsync(CreateMessage());
        await buffer.FailAsync(rowId, "fatal");

        var purgedWithoutFailed = await buffer.PurgeAsync(TimeSpan.FromDays(1), includeFailed: false);
        Assert.Equal(0, purgedWithoutFailed);

        var pending = await buffer.GetPendingMessagesAsync();
        var failed = Assert.Single(pending);
        Assert.Equal(QueueStatus.Failed, failed.Status);

        var oldProcessedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(2)).ToString("O");
        await using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_dataDirectory, "channel_queue.db")}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE channel_queue SET processed_at = @processedAt WHERE row_id = @rowId";
            cmd.Parameters.AddWithValue("@processedAt", oldProcessedAt);
            cmd.Parameters.AddWithValue("@rowId", rowId);
            await cmd.ExecuteNonQueryAsync();
        }

        var purgedWithFailed = await buffer.PurgeAsync(TimeSpan.FromDays(1), includeFailed: true);
        Assert.Equal(1, purgedWithFailed);
        Assert.Empty(await buffer.GetPendingMessagesAsync());
    }

    [Fact]
    public async Task UsingBufferBeforeInitialize_ThrowsHelpfulError()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => buffer.EnqueueAsync(CreateMessage()));
        Assert.Contains("InitializeAsync", ex.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup only; SQLite can briefly hold the file on Windows.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort temp cleanup only.
        }
    }

    private static ChannelMessage CreateMessage()
    {
        return new ChannelMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            ChannelId = "channel-1",
            SenderId = "sender-1",
            SenderDisplayName = "Sender",
            Content = "queued content",
            Timestamp = DateTimeOffset.Parse("2026-04-02T12:34:56Z", CultureInfo.InvariantCulture),
            ThreadId = "thread-1",
            ReplyToMessageId = "reply-1",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["attachmentUrl"] = "https://example.test/file.txt",
                ["kind"] = "unit-test"
            }
        };
    }
}
