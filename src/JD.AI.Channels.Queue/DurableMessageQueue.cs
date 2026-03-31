using System.Text.Json;
using Microsoft.Data.Sqlite;
using JD.AI.Core.Channels;

namespace JD.AI.Channels.Queue;

/// <summary>
/// SQLite WAL-backed durable FIFO queue for <see cref="ChannelMessage"/> ingestion.
/// All writes are transactional; the WAL ensures durability even on crash/power loss.
/// </summary>
public sealed class DurableMessageQueue : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _conn;
    private bool _initialized;

    public DurableMessageQueue(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _dbPath = Path.Combine(dataDirectory, "channel_queue.db");
        _conn = new SqliteConnection($"Data Source={_dbPath}");
    }

    /// <summary>Initializes the DB schema (idempotent — safe to call multiple times).</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _conn.OpenAsync(ct);

        // WAL mode gives us concurrent reads during writes and durability without full flush.
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA busy_timeout=5000;

            CREATE TABLE IF NOT EXISTS channel_queue (
                row_id          INTEGER PRIMARY KEY AUTOINCREMENT,
                message_id      TEXT    NOT NULL UNIQUE,
                channel_id      TEXT    NOT NULL,
                sender_id       TEXT    NOT NULL,
                sender_display  TEXT,
                content         TEXT    NOT NULL,
                timestamp       TEXT    NOT NULL,
                thread_id       TEXT,
                reply_to_id     TEXT,
                attachments     TEXT,
                metadata        TEXT,
                attempt_count   INTEGER NOT NULL DEFAULT 0,
                status          INTEGER NOT NULL DEFAULT 0,
                last_error      TEXT,
                next_retry_after TEXT,
                enqueued_at     TEXT    NOT NULL,
                processed_at    TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_queue_status_next_retry
                ON channel_queue(status, next_retry_after)
                WHERE status IN (0, 1);
            """;
        await cmd.ExecuteNonQueryAsync(ct);

        _initialized = true;
    }

    /// <summary>
    /// Enqueues a <see cref="ChannelMessage"/> immediately.
    /// Returns the assigned <c>row_id</c>.
    /// </summary>
    public async Task<long> EnqueueAsync(ChannelMessage msg, CancellationToken ct = default)
    {
        EnsureInitialized();

        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO channel_queue
                (message_id, channel_id, sender_id, sender_display, content, timestamp,
                 thread_id, reply_to_id, attachments, metadata, attempt_count, status, enqueued_at)
            VALUES
                (@id, @ch, @sid, @sdn, @content, @ts, @tid, @rtid, @att, @meta, 0, 0, @enqueued)
            ON CONFLICT(message_id) DO UPDATE SET attempt_count = attempt_count;
            SELECT last_insert_rowid();
            """;

        cmd.Parameters.AddWithValue("@id", msg.Id);
        cmd.Parameters.AddWithValue("@ch", msg.ChannelId);
        cmd.Parameters.AddWithValue("@sid", msg.SenderId);
        cmd.Parameters.AddWithValue("@sdn", msg.SenderDisplayName ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@content", msg.Content);
        cmd.Parameters.AddWithValue("@ts", msg.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@tid", msg.ThreadId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@rtid", msg.ReplyToMessageId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@att",
            JsonSerializer.Serialize(msg.Attachments.ToList()));
        cmd.Parameters.AddWithValue("@meta",
            JsonSerializer.Serialize(msg.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value)));
        cmd.Parameters.AddWithValue("@enqueued", DateTimeOffset.UtcNow.ToString("O"));

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Atomically claims the next pending message for processing.
    /// Returns null if the queue is empty or all messages are waiting for their backoff.
    /// </summary>
    public async Task<QueuedChannelMessage?> DequeueAsync(CancellationToken ct = default)
    {
        EnsureInitialized();

        await using var tx = await _conn.BeginTransactionAsync(ct);
        try
        {
            await using var selectCmd = _conn.CreateCommand();
            selectCmd.CommandText = """
                SELECT row_id, message_id, channel_id, sender_id, sender_display, content,
                       timestamp, thread_id, reply_to_id, attachments, metadata,
                       attempt_count, status, last_error, next_retry_after, enqueued_at, processed_at
                FROM channel_queue
                WHERE status = 0
                   OR (status = 1 AND next_retry_after <= @now)
                ORDER BY enqueued_at ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED;
                """;
            selectCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));

            QueuedChannelMessage? queued = null;
            await using (var reader = await selectCmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    queued = ReadFromReader(reader);
                }
            }

            if (queued is not null)
            {
                await using var updateCmd = _conn.CreateCommand();
                updateCmd.CommandText = """
                    UPDATE channel_queue
                    SET status = 1,
                        attempt_count = attempt_count + 1,
                        next_retry_after = @next
                    WHERE row_id = @rowId
                    """;
                updateCmd.Parameters.AddWithValue("@rowId", queued.RowId);
                updateCmd.Parameters.AddWithValue("@next",
                    DateTimeOffset.UtcNow.AddSeconds(CalculateBackoffSeconds(queued.AttemptCount + 1)).ToString("O"));
                await updateCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
            return queued;
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Marks a message as permanently completed (removed from the processing pipeline).
    /// </summary>
    public async Task CompleteAsync(long rowId, CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE channel_queue
            SET status = 2, processed_at = @now
            WHERE row_id = @rowId
            """;
        cmd.Parameters.AddWithValue("@rowId", rowId);
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Records a transient failure and schedules the message for retry (if attempts remain).
    /// </summary>
    public async Task RetryAsync(long rowId, string error, CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE channel_queue
            SET status = 0,
                last_error = @err,
                next_retry_after = @next
            WHERE row_id = @rowId
            """;
        cmd.Parameters.AddWithValue("@rowId", rowId);
        cmd.Parameters.AddWithValue("@err", error.Length > 2000 ? error[..2000] : error);
        cmd.Parameters.AddWithValue("@next",
            DateTimeOffset.UtcNow.AddSeconds(CalculateBackoffSeconds(
                GetAttemptCount(rowId).GetAwaiter().GetResult()) + 1).ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Marks a message as permanently failed (no more retries).
    /// </summary>
    public async Task FailAsync(long rowId, string error, CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE channel_queue
            SET status = 3, last_error = @err, next_retry_after = NULL
            WHERE row_id = @rowId
            """;
        cmd.Parameters.AddWithValue("@rowId", rowId);
        cmd.Parameters.AddWithValue("@err", error.Length > 2000 ? error[..2000] : error);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Resets a failed or pending message back to pending for immediate retry.
    /// </summary>
    public async Task ResetAsync(long rowId, CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE channel_queue
            SET status = 0, last_error = NULL, next_retry_after = NULL
            WHERE row_id = @rowId
            """;
        cmd.Parameters.AddWithValue("@rowId", rowId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Returns queue depth broken down by status.
    /// </summary>
    public async Task<QueueStats> GetStatsAsync(CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT status, COUNT(*) FROM channel_queue GROUP BY status;
            """;
        var stats = new QueueStats();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var status = (QueueStatus)reader.GetInt32(0);
            var count = reader.GetInt32(1);
            switch (status)
            {
                case QueueStatus.Pending:    stats.Pending = count; break;
                case QueueStatus.Processing: stats.Processing = count; break;
                case QueueStatus.Completed:  stats.Completed = count; break;
                case QueueStatus.Failed:     stats.Failed = count; break;
            }
        }
        return stats;
    }

    /// <summary>
    /// Returns all non-completed messages ordered by enqueue time.
    /// </summary>
    public async Task<IReadOnlyList<QueuedChannelMessage>> GetPendingMessagesAsync(
        int limit = 100, CancellationToken ct = default)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT row_id, message_id, channel_id, sender_id, sender_display, content,
                   timestamp, thread_id, reply_to_id, attachments, metadata,
                   attempt_count, status, last_error, next_retry_after, enqueued_at, processed_at
            FROM channel_queue
            WHERE status != 2
            ORDER BY enqueued_at ASC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@limit", limit);
        var list = new List<QueuedChannelMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(ReadFromReader(reader));
        return list;
    }

    /// <summary>
    /// Purges completed (and optionally failed) messages older than <paramref name="maxAge"/>.
    /// </summary>
    public async Task<long> PurgeAsync(TimeSpan maxAge, bool includeFailed = false,
        CancellationToken ct = default)
    {
        EnsureInitialized();
        var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge).ToString("O");
        await using var cmd = _conn.CreateCommand();
        if (includeFailed)
        {
            cmd.CommandText = "DELETE FROM channel_queue WHERE status IN (2, 3) AND processed_at < @cutoff";
        }
        else
        {
            cmd.CommandText = "DELETE FROM channel_queue WHERE status = 2 AND processed_at < @cutoff";
        }
        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _conn.DisposeAsync();
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("InitializeAsync must be called before using the queue.");
    }

    private static QueuedChannelMessage ReadFromReader(SqliteDataReader reader) =>
        new()
        {
            RowId = reader.GetInt64(0),
            MessageId = reader.GetString(1),
            ChannelId = reader.GetString(2),
            SenderId = reader.GetString(3),
            SenderDisplayName = reader.IsDBNull(4) ? null : reader.GetString(4),
            Content = reader.GetString(5),
            Timestamp = DateTimeOffset.Parse(reader.GetString(6)),
            ThreadId = reader.IsDBNull(7) ? null : reader.GetString(7),
            ReplyToMessageId = reader.IsDBNull(8) ? null : reader.GetString(8),
            SerializedAttachments = reader.IsDBNull(9) ? null : reader.GetString(9),
            SerializedMetadata = reader.IsDBNull(10) ? null : reader.GetString(10),
            AttemptCount = reader.GetInt32(11),
            Status = (QueueStatus)reader.GetInt32(12),
            LastError = reader.IsDBNull(13) ? null : reader.GetString(13),
            NextRetryAfter = reader.IsDBNull(14) ? default : DateTimeOffset.Parse(reader.GetString(14)),
            EnqueuedAt = DateTimeOffset.Parse(reader.GetString(15)),
            ProcessedAt = reader.IsDBNull(16) ? null : DateTimeOffset.Parse(reader.GetString(16))
        };

    private static int CalculateBackoffSeconds(int attempt) =>
        (int)Math.Min(30 * Math.Pow(2, attempt - 1), 300); // 2, 4, 8, 16, 30... capped at 5 min

    private async Task<int> GetAttemptCount(long rowId)
    {
        EnsureInitialized();
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT attempt_count FROM channel_queue WHERE row_id = @rowId";
        cmd.Parameters.AddWithValue("@rowId", rowId);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}

public sealed class QueueStats
{
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Total => Pending + Processing + Completed + Failed;
}
