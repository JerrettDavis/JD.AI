using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using JD.AI.Core.Agents;
using Microsoft.Data.Sqlite;

namespace JD.AI.Core.Sessions;

/// <summary>
/// SQLite-backed session store. Handles all CRUD against ~/.jdai/sessions.db.
/// </summary>
public sealed class SessionStore : IDisposable
{
    private static readonly ActivitySource SessionActivity = new("JD.AI.Sessions");

    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public SessionStore(string? dbPath = null)
    {
        dbPath ??= GetDefaultDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
    }

    private static string GetDefaultDbPath() => Config.DataDirectories.SessionsDb;

    private async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is { State: System.Data.ConnectionState.Open })
            return _connection;

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync().ConfigureAwait(false);
        return _connection;
    }

    /// <summary>Create tables if they don't exist.</summary>
    public async Task InitializeAsync()
    {
        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                id              TEXT PRIMARY KEY,
                name            TEXT,
                project_path    TEXT NOT NULL,
                project_hash    TEXT NOT NULL,
                model_id        TEXT,
                provider_name   TEXT,
                created_at      TEXT NOT NULL,
                updated_at      TEXT NOT NULL,
                total_tokens    INTEGER DEFAULT 0,
                message_count   INTEGER DEFAULT 0,
                is_active       INTEGER DEFAULT 1
            );
            CREATE TABLE IF NOT EXISTS turns (
                id              TEXT PRIMARY KEY,
                session_id      TEXT NOT NULL REFERENCES sessions(id),
                turn_index      INTEGER NOT NULL,
                role            TEXT NOT NULL,
                content         TEXT,
                thinking_text   TEXT,
                model_id        TEXT,
                provider_name   TEXT,
                tokens_in       INTEGER DEFAULT 0,
                tokens_out      INTEGER DEFAULT 0,
                duration_ms     INTEGER DEFAULT 0,
                created_at      TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS tool_calls (
                id              TEXT PRIMARY KEY,
                turn_id         TEXT NOT NULL REFERENCES turns(id),
                tool_name       TEXT NOT NULL,
                arguments       TEXT,
                result          TEXT,
                status          TEXT DEFAULT 'ok',
                duration_ms     INTEGER DEFAULT 0,
                created_at      TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS files_touched (
                id              TEXT PRIMARY KEY,
                turn_id         TEXT NOT NULL REFERENCES turns(id),
                file_path       TEXT NOT NULL,
                operation       TEXT NOT NULL,
                created_at      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_project ON sessions(project_hash);
            CREATE INDEX IF NOT EXISTS idx_sessions_updated ON sessions(updated_at DESC);
            CREATE INDEX IF NOT EXISTS idx_turns_session ON turns(session_id, turn_index);
            CREATE INDEX IF NOT EXISTS idx_tool_calls_turn ON tool_calls(turn_id);
            CREATE INDEX IF NOT EXISTS idx_files_touched_turn ON files_touched(turn_id);
            """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Migrate: add model_switch_history and fork_points columns if missing
        await MigrateAddColumnAsync(conn, "sessions", "model_switch_history", "TEXT").ConfigureAwait(false);
        await MigrateAddColumnAsync(conn, "sessions", "fork_points", "TEXT").ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Parameters are internal column names, not user input")]
    private static async Task MigrateAddColumnAsync(SqliteConnection conn, string table, string column, string type)
    {
        using var pragma = conn.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({table})";
        using var reader = await pragma.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
                return;
        }
        reader.Close();

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
        try
        {
            await alter.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (SqliteException ex) when (IsDuplicateColumnError(ex, column))
        {
            // Concurrent initialization can race between PRAGMA check and ALTER.
            // Treat "duplicate column" as success to keep initialization idempotent.
        }
    }

    private static bool IsDuplicateColumnError(SqliteException ex, string column) =>
        ex.SqliteErrorCode == 1 &&
        ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase) &&
        ex.Message.Contains(column, StringComparison.OrdinalIgnoreCase);

    public async Task CreateSessionAsync(SessionInfo session)
    {
        using var activity = SessionActivity.StartActivity("jdai.session.create");
        activity?.SetTag("jdai.session.id", session.Id);
        activity?.SetTag("jdai.session.project_hash", session.ProjectHash);

        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (id, name, project_path, project_hash, model_id, provider_name,
                                  created_at, updated_at, total_tokens, message_count, is_active,
                                  model_switch_history, fork_points)
            VALUES ($id, $name, $pp, $ph, $mid, $pn, $ca, $ua, $tt, $mc, $ia, $msh, $fp)
            """;
        cmd.Parameters.AddWithValue("$id", session.Id);
        cmd.Parameters.AddWithValue("$name", (object?)session.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pp", session.ProjectPath);
        cmd.Parameters.AddWithValue("$ph", session.ProjectHash);
        cmd.Parameters.AddWithValue("$mid", (object?)session.ModelId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pn", (object?)session.ProviderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ca", session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$ua", session.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$tt", session.TotalTokens);
        cmd.Parameters.AddWithValue("$mc", session.MessageCount);
        cmd.Parameters.AddWithValue("$ia", session.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$msh", SerializeJson(session.ModelSwitchHistory));
        cmd.Parameters.AddWithValue("$fp", SerializeJson(session.ForkPoints));
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task UpdateSessionAsync(SessionInfo session)
    {
        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE sessions SET name=$name, updated_at=$ua, total_tokens=$tt,
                                message_count=$mc, is_active=$ia,
                                model_switch_history=$msh, fork_points=$fp
            WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$id", session.Id);
        cmd.Parameters.AddWithValue("$name", (object?)session.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ua", session.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$tt", session.TotalTokens);
        cmd.Parameters.AddWithValue("$mc", session.MessageCount);
        cmd.Parameters.AddWithValue("$ia", session.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$msh", SerializeJson(session.ModelSwitchHistory));
        cmd.Parameters.AddWithValue("$fp", SerializeJson(session.ForkPoints));
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task SaveTurnAsync(TurnRecord turn)
    {
        using var activity = SessionActivity.StartActivity("jdai.session.save_turn");
        activity?.SetTag("jdai.session.id", turn.SessionId);
        activity?.SetTag("jdai.turn.index", turn.TurnIndex);
        activity?.SetTag("jdai.turn.role", turn.Role);

        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR REPLACE INTO turns (id, session_id, turn_index, role, content, thinking_text,
                                              model_id, provider_name, tokens_in, tokens_out, duration_ms, created_at)
                VALUES ($id, $sid, $ti, $role, $content, $think, $mid, $pn, $tin, $tout, $dur, $ca)
                """;
            cmd.Parameters.AddWithValue("$id", turn.Id);
            cmd.Parameters.AddWithValue("$sid", turn.SessionId);
            cmd.Parameters.AddWithValue("$ti", turn.TurnIndex);
            cmd.Parameters.AddWithValue("$role", turn.Role);
            cmd.Parameters.AddWithValue("$content", (object?)turn.Content ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$think", (object?)turn.ThinkingText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mid", (object?)turn.ModelId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pn", (object?)turn.ProviderName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$tin", turn.TokensIn);
            cmd.Parameters.AddWithValue("$tout", turn.TokensOut);
            cmd.Parameters.AddWithValue("$dur", turn.DurationMs);
            cmd.Parameters.AddWithValue("$ca", turn.CreatedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        foreach (var tc in turn.ToolCalls)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR REPLACE INTO tool_calls (id, turn_id, tool_name, arguments, result, status, duration_ms, created_at)
                VALUES ($id, $tid, $tn, $args, $res, $st, $dur, $ca)
                """;
            cmd.Parameters.AddWithValue("$id", tc.Id);
            cmd.Parameters.AddWithValue("$tid", tc.TurnId);
            cmd.Parameters.AddWithValue("$tn", tc.ToolName);
            cmd.Parameters.AddWithValue("$args", (object?)tc.Arguments ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$res", (object?)tc.Result ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$st", tc.Status);
            cmd.Parameters.AddWithValue("$dur", tc.DurationMs);
            cmd.Parameters.AddWithValue("$ca", tc.CreatedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        foreach (var ft in turn.FilesTouched)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT OR REPLACE INTO files_touched (id, turn_id, file_path, operation, created_at)
                VALUES ($id, $tid, $fp, $op, $ca)
                """;
            cmd.Parameters.AddWithValue("$id", ft.Id);
            cmd.Parameters.AddWithValue("$tid", ft.TurnId);
            cmd.Parameters.AddWithValue("$fp", ft.FilePath);
            cmd.Parameters.AddWithValue("$op", ft.Operation);
            cmd.Parameters.AddWithValue("$ca", ft.CreatedAt.ToString("O"));
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await tx.CommitAsync().ConfigureAwait(false);
    }

    public async Task<SessionInfo?> GetSessionAsync(string id)
    {
        using var activity = SessionActivity.StartActivity("jdai.session.load");
        activity?.SetTag("jdai.session.id", id);

        var conn = await GetConnectionAsync().ConfigureAwait(false);
        SessionInfo? session = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM sessions WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
                session = ReadSession(reader);
        }

        if (session == null) return null;

        // Load turns
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM turns WHERE session_id=$sid ORDER BY turn_index";
            cmd.Parameters.AddWithValue("$sid", id);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                session.Turns.Add(ReadTurn(reader));
        }

        // Load tool calls and files for each turn
        foreach (var turn in session.Turns)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM tool_calls WHERE turn_id=$tid ORDER BY created_at";
                cmd.Parameters.AddWithValue("$tid", turn.Id);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                    turn.ToolCalls.Add(ReadToolCall(reader));
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM files_touched WHERE turn_id=$tid ORDER BY created_at";
                cmd.Parameters.AddWithValue("$tid", turn.Id);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                    turn.FilesTouched.Add(ReadFileTouch(reader));
            }
        }

        return session;
    }

    public async Task<ReadOnlyCollection<SessionInfo>> ListSessionsAsync(string? projectHash = null, int limit = 20)
    {
        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var cmd = conn.CreateCommand();

        if (projectHash != null)
        {
            cmd.CommandText = "SELECT * FROM sessions WHERE project_hash=$ph ORDER BY updated_at DESC LIMIT $lim";
            cmd.Parameters.AddWithValue("$ph", projectHash);
        }
        else
        {
            cmd.CommandText = "SELECT * FROM sessions ORDER BY updated_at DESC LIMIT $lim";
        }
        cmd.Parameters.AddWithValue("$lim", limit);

        var sessions = new List<SessionInfo>();
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
            sessions.Add(ReadSession(reader));

        return sessions.AsReadOnly();
    }

    public async Task DeleteTurnsAfterAsync(string sessionId, int turnIndex)
    {
        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var tx = conn.BeginTransaction();

        // Get turn IDs to delete
        var turnIds = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id FROM turns WHERE session_id=$sid AND turn_index > $ti";
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.Parameters.AddWithValue("$ti", turnIndex);
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
                turnIds.Add(reader.GetString(0));
        }

        foreach (var turnId in turnIds)
        {
            using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = tx;
            cmd1.CommandText = "DELETE FROM files_touched WHERE turn_id=$tid";
            cmd1.Parameters.AddWithValue("$tid", turnId);
            await cmd1.ExecuteNonQueryAsync().ConfigureAwait(false);

            using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = tx;
            cmd2.CommandText = "DELETE FROM tool_calls WHERE turn_id=$tid";
            cmd2.Parameters.AddWithValue("$tid", turnId);
            await cmd2.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM turns WHERE session_id=$sid AND turn_index > $ti";
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.Parameters.AddWithValue("$ti", turnIndex);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await tx.CommitAsync().ConfigureAwait(false);
    }

    public async Task CloseSessionAsync(string id)
    {
        var conn = await GetConnectionAsync().ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE sessions SET is_active=0, updated_at=$ua WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$ua", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // ── Row readers ─────────────────────────────────────

    private static SessionInfo ReadSession(SqliteDataReader r)
    {
        var session = new SessionInfo
        {
            Id = r.GetString(r.GetOrdinal("id")),
            Name = r.IsDBNull(r.GetOrdinal("name")) ? null : r.GetString(r.GetOrdinal("name")),
            ProjectPath = r.GetString(r.GetOrdinal("project_path")),
            ProjectHash = r.GetString(r.GetOrdinal("project_hash")),
            ModelId = r.IsDBNull(r.GetOrdinal("model_id")) ? null : r.GetString(r.GetOrdinal("model_id")),
            ProviderName = r.IsDBNull(r.GetOrdinal("provider_name")) ? null : r.GetString(r.GetOrdinal("provider_name")),
            CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
            UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("updated_at"))),
            TotalTokens = r.GetInt64(r.GetOrdinal("total_tokens")),
            MessageCount = r.GetInt32(r.GetOrdinal("message_count")),
            IsActive = r.GetInt32(r.GetOrdinal("is_active")) == 1,
        };

        var mshOrd = r.GetOrdinal("model_switch_history");
        if (!r.IsDBNull(mshOrd))
        {
            var list = JsonSerializer.Deserialize<List<ModelSwitchRecord>>(r.GetString(mshOrd));
            if (list != null) session.ModelSwitchHistory.AddRange(list);
        }

        var fpOrd = r.GetOrdinal("fork_points");
        if (!r.IsDBNull(fpOrd))
        {
            var list = JsonSerializer.Deserialize<List<ForkPoint>>(r.GetString(fpOrd));
            if (list != null) session.ForkPoints.AddRange(list);
        }

        return session;
    }

    private static TurnRecord ReadTurn(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("id")),
        SessionId = r.GetString(r.GetOrdinal("session_id")),
        TurnIndex = r.GetInt32(r.GetOrdinal("turn_index")),
        Role = r.GetString(r.GetOrdinal("role")),
        Content = r.IsDBNull(r.GetOrdinal("content")) ? null : r.GetString(r.GetOrdinal("content")),
        ThinkingText = r.IsDBNull(r.GetOrdinal("thinking_text")) ? null : r.GetString(r.GetOrdinal("thinking_text")),
        ModelId = r.IsDBNull(r.GetOrdinal("model_id")) ? null : r.GetString(r.GetOrdinal("model_id")),
        ProviderName = r.IsDBNull(r.GetOrdinal("provider_name")) ? null : r.GetString(r.GetOrdinal("provider_name")),
        TokensIn = r.GetInt64(r.GetOrdinal("tokens_in")),
        TokensOut = r.GetInt64(r.GetOrdinal("tokens_out")),
        DurationMs = r.GetInt64(r.GetOrdinal("duration_ms")),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
    };

    private static ToolCallRecord ReadToolCall(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("id")),
        TurnId = r.GetString(r.GetOrdinal("turn_id")),
        ToolName = r.GetString(r.GetOrdinal("tool_name")),
        Arguments = r.IsDBNull(r.GetOrdinal("arguments")) ? null : r.GetString(r.GetOrdinal("arguments")),
        Result = r.IsDBNull(r.GetOrdinal("result")) ? null : r.GetString(r.GetOrdinal("result")),
        Status = r.GetString(r.GetOrdinal("status")),
        DurationMs = r.GetInt64(r.GetOrdinal("duration_ms")),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
    };

    private static FileTouchRecord ReadFileTouch(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("id")),
        TurnId = r.GetString(r.GetOrdinal("turn_id")),
        FilePath = r.GetString(r.GetOrdinal("file_path")),
        Operation = r.GetString(r.GetOrdinal("operation")),
        CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
    };

    private static string SerializeJson<T>(T value) =>
        JsonSerializer.Serialize(value);

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
        SqliteConnection.ClearAllPools();
    }
}
