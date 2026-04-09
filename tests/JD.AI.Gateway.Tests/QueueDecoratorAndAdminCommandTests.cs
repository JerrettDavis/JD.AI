using System.Globalization;
using JD.AI.Channels.Queue;
using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Gateway.Tests;

public sealed class QueueDecoratorAndAdminCommandTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), $"jdai-queue-decorator-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task RegisterCommandsAsync_RegistersQueueAdminCommands()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        var inner = new FakeChannel();
        await using var decorator = new DurableQueueChannelDecorator(inner, buffer, NullLogger.Instance);
        var registry = new CommandRegistry();

        await decorator.RegisterCommandsAsync(registry);

        Assert.NotNull(registry.GetCommand("queue-peek"));
        Assert.NotNull(registry.GetCommand("queue-retry"));
        Assert.NotNull(registry.GetCommand("queue-purge"));
        Assert.Equal(3, registry.Commands.Count);
    }

    [Fact]
    public async Task MessageReceived_WithUninitializedQueue_FallsBackToDirectDispatch()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        var inner = new FakeChannel();
        await using var decorator = new DurableQueueChannelDecorator(inner, buffer, NullLogger.Instance);
        var delivered = new TaskCompletionSource<ChannelMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        decorator.MessageReceived += message =>
        {
            delivered.TrySetResult(message);
            return Task.CompletedTask;
        };

        var message = CreateMessage(content: "fallback path");
        await inner.EmitAsync(message);

        var received = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(message.Id, received.Id);
        Assert.Equal(message.Content, received.Content);
    }

    [Fact]
    public async Task MessageReceived_WithMultipleSubscribers_WiresInnerHandlerOnlyOnce()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        var inner = new FakeChannel();
        await using var decorator = new DurableQueueChannelDecorator(inner, buffer, NullLogger.Instance);
        await decorator.StartAsync(CancellationToken.None);

        var subscriberOneCalls = 0;
        var subscriberTwoCalls = 0;
        var allDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        decorator.MessageReceived += _ =>
        {
            if (Interlocked.Increment(ref subscriberOneCalls) == 1 && Volatile.Read(ref subscriberTwoCalls) == 1)
            {
                allDelivered.TrySetResult();
            }

            return Task.CompletedTask;
        };

        decorator.MessageReceived += _ =>
        {
            if (Interlocked.Increment(ref subscriberTwoCalls) == 1 && Volatile.Read(ref subscriberOneCalls) == 1)
            {
                allDelivered.TrySetResult();
            }

            return Task.CompletedTask;
        };

        await inner.EmitAsync(CreateMessage(content: "single dispatch"));
        await allDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, subscriberOneCalls);
        Assert.Equal(1, subscriberTwoCalls);

        var stats = await buffer.GetStatsAsync();
        Assert.Equal(1, stats.Completed);
        Assert.Equal(1, stats.Total);
    }

    [Fact]
    public async Task QueuePeekCommand_ShowsOverviewAndPendingEntries()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();

        var pendingRowId = await buffer.EnqueueAsync(CreateMessage(content: new string('p', 80)));
        var failedRowId = await buffer.EnqueueAsync(CreateMessage(id: "failed-message", content: "failed content"));
        await buffer.FailAsync(failedRowId, "fatal problem");

        var command = new QueuePeekCommand(buffer);
        var result = await command.ExecuteAsync(CreateContext("queue-peek"));

        Assert.True(result.Success);
        Assert.Contains("**Queue Overview**", result.Content, StringComparison.Ordinal);
        Assert.Contains($"[{pendingRowId}] ⏳ pending", result.Content, StringComparison.Ordinal);
        Assert.Contains($"[{failedRowId}] ❌ failed (0 attempts)", result.Content, StringComparison.Ordinal);
        Assert.Contains(new string('p', 60) + "…", result.Content, StringComparison.Ordinal);
        Assert.Contains("From: Sender | failed content", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueRetryCommand_RequeuesMessageAndRejectsInvalidIds()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();

        var rowId = await buffer.EnqueueAsync(CreateMessage());
        await buffer.FailAsync(rowId, "fatal");
        var command = new QueueRetryCommand(buffer);

        var invalid = await command.ExecuteAsync(CreateContext("queue-retry", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["row_id"] = "abc"
        }));

        Assert.False(invalid.Success);
        Assert.Contains("Invalid row ID", invalid.Content, StringComparison.Ordinal);

        var success = await command.ExecuteAsync(CreateContext("queue-retry", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["row_id"] = rowId.ToString(CultureInfo.InvariantCulture)
        }));

        Assert.True(success.Success);
        Assert.Contains($"Message {rowId} has been requeued", success.Content, StringComparison.Ordinal);

        var pending = await buffer.GetPendingMessagesAsync();
        var requeued = Assert.Single(pending);
        Assert.Equal(QueueStatus.Pending, requeued.Status);
        Assert.Null(requeued.LastError);
        Assert.Equal(default, requeued.NextRetryAfter);
    }

    [Theory]
    [InlineData("12", false, "older than 12.")]
    [InlineData("24h", true, "completed and failed messages older than 24h.")]
    public async Task QueuePurgeCommand_ParsesDurationsAndPurgesMatchingRows(string duration, bool includeFailed, string expectedSuffix)
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();

        var completedRowId = await buffer.EnqueueAsync(CreateMessage(id: "completed-message"));
        await buffer.CompleteAsync(completedRowId);

        var failedRowId = await buffer.EnqueueAsync(CreateMessage(id: "failed-message"));
        await buffer.FailAsync(failedRowId, "fatal");

        await SetProcessedAtAsync(completedRowId, DateTimeOffset.UtcNow.AddDays(-10));
        await SetProcessedAtAsync(failedRowId, DateTimeOffset.UtcNow.AddDays(-10));

        var command = new QueuePurgeCommand(buffer);
        var result = await command.ExecuteAsync(CreateContext(
            "queue-purge",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["duration"] = duration,
                ["include_failed"] = includeFailed.ToString()
            }));

        Assert.True(result.Success);
        Assert.Contains(expectedSuffix, result.Content, StringComparison.Ordinal);

        var remaining = await buffer.GetPendingMessagesAsync();
        if (includeFailed)
        {
            Assert.Empty(remaining);
        }
        else
        {
            var failed = Assert.Single(remaining);
            Assert.Equal(QueueStatus.Failed, failed.Status);
        }
    }

    [Fact]
    public async Task QueuePurgeCommand_RejectsUnknownDurationFormat()
    {
        await using var buffer = new DiscordMessageBuffer(_dataDirectory);
        await buffer.InitializeAsync();
        var command = new QueuePurgeCommand(buffer);

        var result = await command.ExecuteAsync(CreateContext("queue-purge", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["duration"] = "later"
        }));

        Assert.False(result.Success);
        Assert.Contains("Unknown duration format", result.Content, StringComparison.Ordinal);
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task SetProcessedAtAsync(long rowId, DateTimeOffset value)
    {
        await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(_dataDirectory, "channel_queue.db")}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE channel_queue SET processed_at = @processedAt WHERE row_id = @rowId";
        cmd.Parameters.AddWithValue("@processedAt", value.ToString("O"));
        cmd.Parameters.AddWithValue("@rowId", rowId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static CommandContext CreateContext(string name, IReadOnlyDictionary<string, string>? arguments = null)
    {
        return new CommandContext
        {
            CommandName = name,
            InvokerId = "user-1",
            InvokerDisplayName = "Tester",
            ChannelId = "channel-1",
            ChannelType = "discord",
            Arguments = arguments ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    private static ChannelMessage CreateMessage(string? id = null, string content = "queued content")
    {
        return new ChannelMessage
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            ChannelId = "channel-1",
            SenderId = "sender-1",
            SenderDisplayName = "Sender",
            Content = content,
            Timestamp = DateTimeOffset.Parse("2026-04-02T12:34:56Z", CultureInfo.InvariantCulture),
            ThreadId = "thread-1",
            ReplyToMessageId = "reply-1",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "unit-test"
            }
        };
    }

    private sealed class FakeChannel : IChannel
    {
        public string ChannelType => "discord";
        public string DisplayName => "Fake Discord";
        public bool IsConnected { get; private set; }

        public event Func<ChannelMessage, Task>? MessageReceived;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default) => Task.CompletedTask;

        public Task SendMessageWithAttachmentsAsync(string conversationId, string? content, IReadOnlyList<OutboundAttachment> attachments, CancellationToken ct = default) => Task.CompletedTask;

        public Task ReactAsync(string conversationId, string messageId, string emoji, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task EmitAsync(ChannelMessage message) => MessageReceived?.Invoke(message) ?? Task.CompletedTask;
    }
}
