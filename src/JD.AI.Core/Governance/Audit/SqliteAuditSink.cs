using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// An <see cref="IQueryableAuditSink"/> that persists audit events to a SQLite database.
/// Supports tamper-evident event chaining and all <see cref="AuditQuery"/> filters.
/// </summary>
public sealed class SqliteAuditSink : IQueryableAuditSink, IAsyncDisposable
{
    private readonly string _connectionString;
    private long _count;

    public string Name => "sqlite";

    public long Count => Interlocked.Read(ref _count);

    public SqliteAuditSink(string dbPath)
    {
        ArgumentNullException.ThrowIfNull(dbPath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS audit_events (
                rowid       INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id    TEXT    NOT NULL UNIQUE,
                timestamp   TEXT    NOT NULL,
                user_id     TEXT,
                session_id  TEXT,
                trace_id    TEXT,
                action      TEXT    NOT NULL,
                resource    TEXT,
                detail      TEXT,
                severity    INTEGER NOT NULL DEFAULT 1,
                policy_result TEXT,
                tool_name   TEXT,
                tool_args   TEXT,
                tool_result TEXT,
                duration_ms INTEGER,
                previous_hash TEXT,
                payload     TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_events(timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_audit_session   ON audit_events(session_id);
            CREATE INDEX IF NOT EXISTS idx_audit_user      ON audit_events(user_id);
            CREATE INDEX IF NOT EXISTS idx_audit_action    ON audit_events(action COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS idx_audit_severity  ON audit_events(severity);
            """;
        cmd.ExecuteNonQuery();

        // Seed the count
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM audit_events;";
        var result = countCmd.ExecuteScalar();
        Interlocked.Exchange(ref _count, result is long l ? l : Convert.ToInt64(result));
    }

    /// <inheritdoc/>
    public async Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        await using var conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            INSERT OR IGNORE INTO audit_events
                (event_id, timestamp, user_id, session_id, trace_id, action, resource,
                 detail, severity, policy_result, tool_name, tool_args, tool_result,
                 duration_ms, previous_hash, payload)
            VALUES
                (@eid, @ts, @uid, @sid, @tid, @act, @res,
                 @det, @sev, @pol, @tn, @ta, @tr,
                 @dur, @ph, @payload);
            """;

        AddParameter(cmd, "@eid", evt.EventId);
        AddParameter(cmd, "@ts", evt.Timestamp.ToString("O"));
        AddParameter(cmd, "@uid", (object?)evt.UserId ?? DBNull.Value);
        AddParameter(cmd, "@sid", (object?)evt.SessionId ?? DBNull.Value);
        AddParameter(cmd, "@tid", (object?)evt.TraceId ?? DBNull.Value);
        AddParameter(cmd, "@act", evt.Action);
        AddParameter(cmd, "@res", (object?)evt.Resource ?? DBNull.Value);
        AddParameter(cmd, "@det", (object?)evt.Detail ?? DBNull.Value);
        AddParameter(cmd, "@sev", (int)evt.Severity);
        AddParameter(cmd, "@pol", evt.PolicyResult.HasValue ? (object)evt.PolicyResult.Value.ToString() : DBNull.Value);
        AddParameter(cmd, "@tn", (object?)evt.ToolName ?? DBNull.Value);
        AddParameter(cmd, "@ta", (object?)evt.ToolArguments ?? DBNull.Value);
        AddParameter(cmd, "@tr", (object?)evt.ToolResult ?? DBNull.Value);
        AddParameter(cmd, "@dur", evt.DurationMs.HasValue ? (object)evt.DurationMs.Value : DBNull.Value);
        AddParameter(cmd, "@ph", (object?)evt.PreviousHash ?? DBNull.Value);
        AddParameter(cmd, "@payload", JsonSerializer.Serialize(evt));

        var rows = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (rows > 0)
            Interlocked.Increment(ref _count);
    }

    /// <inheritdoc/>
    public async Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Min(query.Limit > 0 ? query.Limit : 50, 1000);
        var offset = Math.Max(query.Offset, 0);

        await using var conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

        var (where, parameters) = BuildWhereClause(query);

        // Count total
        await using var countCmd = conn.CreateCommand();
        // CA2100: where clause uses only parameterized conditions built by BuildWhereClause — no injection risk
#pragma warning disable CA2100
        countCmd.CommandText = $"SELECT COUNT(*) FROM audit_events {where};";
        foreach (var (name, value) in parameters)
            AddParameter(countCmd, name, value);
#pragma warning restore CA2100

        var totalRaw = await countCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var total = totalRaw is long l ? l : Convert.ToInt64(totalRaw);

        // Fetch events
        await using var dataCmd = conn.CreateCommand();
        // CA2100: limit and offset are integer-sanitized above (Math.Min/Max) — no injection risk
#pragma warning disable CA2100
        dataCmd.CommandText = $"""
            SELECT payload FROM audit_events
            {where}
            ORDER BY timestamp DESC
            LIMIT {limit} OFFSET {offset};
            """;
#pragma warning restore CA2100
        foreach (var (name, value) in parameters)
            AddParameter(dataCmd, name, value);

        var events = new List<AuditEvent>();
        await using var reader = await dataCmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var json = reader.GetString(0);
            var evt = JsonSerializer.Deserialize<AuditEvent>(json);
            if (evt is not null)
                events.Add(evt);
        }

        return new AuditQueryResult { Events = events, TotalCount = total };
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── helpers ──────────────────────────────────────────────────────────────

    private static (string where, List<(string name, object value)> parameters) BuildWhereClause(AuditQuery q)
    {
        var conditions = new List<string>();
        var parameters = new List<(string, object)>();

        if (q.Action is not null)
        {
            conditions.Add("action = @action COLLATE NOCASE");
            parameters.Add(("@action", q.Action));
        }

        if (q.MinSeverity.HasValue)
        {
            conditions.Add("severity >= @sev");
            parameters.Add(("@sev", (int)q.MinSeverity.Value));
        }

        if (q.SessionId is not null)
        {
            conditions.Add("session_id = @sid");
            parameters.Add(("@sid", q.SessionId));
        }

        if (q.UserId is not null)
        {
            conditions.Add("user_id = @uid");
            parameters.Add(("@uid", q.UserId));
        }

        if (q.Resource is not null)
        {
            conditions.Add("resource LIKE @res COLLATE NOCASE");
            parameters.Add(("@res", $"%{q.Resource}%"));
        }

        if (q.From.HasValue)
        {
            conditions.Add("timestamp >= @from");
            parameters.Add(("@from", q.From.Value.ToString("O")));
        }

        if (q.Until.HasValue)
        {
            conditions.Add("timestamp < @until");
            parameters.Add(("@until", q.Until.Value.ToString("O")));
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        return (where, parameters);
    }

    private static void AddParameter(SqliteCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        await pragma.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return conn;
    }
}
