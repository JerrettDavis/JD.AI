using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Infrastructure;
using Microsoft.Data.Sqlite;

namespace JD.AI.Core.Usage;

/// <summary>
/// SQLite-backed usage metering service. Stores turn-level usage records
/// in a dedicated usage.db and provides aggregation queries.
/// </summary>
public sealed class SqliteUsageMeter : IUsageMeter, IAsyncDisposable
{
    private readonly SqliteConnection _db;
    private readonly CostRateProvider _costRates;
    private readonly BudgetConfig _budget;
    private bool _initialized;
    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Indented;

    public SqliteUsageMeter(string dbPath, CostRateProvider? costRates = null, BudgetConfig? budget = null)
    {
        _costRates = costRates ?? new CostRateProvider();
        _budget = budget ?? new BudgetConfig();

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _db = new SqliteConnection($"Data Source={dbPath}");
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _db.OpenAsync(ct);
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS usage_turns (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id      TEXT NOT NULL,
                provider_id     TEXT NOT NULL,
                model_id        TEXT NOT NULL,
                prompt_tokens   INTEGER NOT NULL DEFAULT 0,
                completion_tokens INTEGER NOT NULL DEFAULT 0,
                tool_calls      INTEGER NOT NULL DEFAULT 0,
                duration_ms     INTEGER NOT NULL DEFAULT 0,
                estimated_cost  REAL NOT NULL DEFAULT 0,
                project_path    TEXT,
                trace_id        TEXT,
                timestamp       TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_usage_session ON usage_turns(session_id);
            CREATE INDEX IF NOT EXISTS idx_usage_provider ON usage_turns(provider_id);
            CREATE INDEX IF NOT EXISTS idx_usage_timestamp ON usage_turns(timestamp);
            CREATE INDEX IF NOT EXISTS idx_usage_project ON usage_turns(project_path);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        _initialized = true;
    }

    public async Task RecordTurnAsync(TurnUsageRecord record, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var cost = _costRates.CalculateCost(
            record.ProviderId, record.ModelId,
            record.PromptTokens, record.CompletionTokens);

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage_turns
                (session_id, provider_id, model_id, prompt_tokens, completion_tokens,
                 tool_calls, duration_ms, estimated_cost, project_path, trace_id, timestamp)
            VALUES
                ($sid, $pid, $mid, $pt, $ct, $tc, $dm, $ec, $pp, $tid, $ts)
            """;

        cmd.Parameters.AddWithValue("$sid", record.SessionId);
        cmd.Parameters.AddWithValue("$pid", record.ProviderId);
        cmd.Parameters.AddWithValue("$mid", record.ModelId);
        cmd.Parameters.AddWithValue("$pt", record.PromptTokens);
        cmd.Parameters.AddWithValue("$ct", record.CompletionTokens);
        cmd.Parameters.AddWithValue("$tc", record.ToolCalls);
        cmd.Parameters.AddWithValue("$dm", record.DurationMs);
        cmd.Parameters.AddWithValue("$ec", (double)cost);
        cmd.Parameters.AddWithValue("$pp", record.ProjectPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$tid", record.TraceId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ts", record.Timestamp.ToString("o", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<UsageSummary> GetSessionUsageAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        return await QueryUsageAsync("WHERE session_id = $filter", "$filter", sessionId, ct);
    }

    public async Task<UsageSummary> GetProjectUsageAsync(string projectPath, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        return await QueryUsageAsync("WHERE project_path = $filter", "$filter", projectPath, ct);
    }

    public async Task<UsageSummary> GetPeriodUsageAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT provider_id,
                   SUM(prompt_tokens) as pt, SUM(completion_tokens) as ct_sum,
                   COUNT(*) as turns, SUM(tool_calls) as tc, SUM(estimated_cost) as cost
            FROM usage_turns
            WHERE timestamp >= $from AND timestamp <= $to
            GROUP BY provider_id
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to", until.ToString("o", CultureInfo.InvariantCulture));

        return await ReadSummaryAsync(cmd, ct);
    }

    public async Task<UsageSummary> GetTotalUsageAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        return await QueryUsageAsync("", null, null, ct);
    }

    public async Task<BudgetStatus> CheckBudgetAsync(BudgetPeriod period = BudgetPeriod.Monthly, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var (from, to) = GetPeriodRange(period);
        var usage = await GetPeriodUsageAsync(from, to, ct);
        var spent = usage.EstimatedCostUsd;

        var limit = period switch
        {
            BudgetPeriod.Daily => _budget.DailyLimitUsd,
            BudgetPeriod.Weekly => _budget.WeeklyLimitUsd,
            BudgetPeriod.Monthly => _budget.MonthlyLimitUsd,
            BudgetPeriod.Session => _budget.SessionLimitUsd,
            _ => null,
        };

        var warningThreshold = limit.HasValue ? limit.Value * _budget.WarningThresholdPercent : (decimal?)null;

        return new BudgetStatus
        {
            SpentUsd = spent,
            LimitUsd = limit,
            WarningThresholdUsd = warningThreshold,
            Period = period,
        };
    }

    public async Task<string> ExportAsync(UsageExportFormat format, DateTimeOffset? from = null, DateTimeOffset? until = null, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        await using var cmd = _db.CreateCommand();
        var where = new StringBuilder();
        if (from.HasValue)
        {
            where.Append(" WHERE timestamp >= $from");
            cmd.Parameters.AddWithValue("$from", from.Value.ToString("o", CultureInfo.InvariantCulture));
        }
        if (until.HasValue)
        {
            where.Append(from.HasValue ? " AND" : " WHERE");
            where.Append(" timestamp <= $to");
            cmd.Parameters.AddWithValue("$to", until.Value.ToString("o", CultureInfo.InvariantCulture));
        }

#pragma warning disable CA2100 // SQL constructed from static strings only, no user input
        cmd.CommandText = $"""
            SELECT session_id, provider_id, model_id, prompt_tokens, completion_tokens,
                   tool_calls, duration_ms, estimated_cost, project_path, trace_id, timestamp
            FROM usage_turns
            {where}
            ORDER BY timestamp ASC
            """;
#pragma warning restore CA2100

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object>>();

        while (await reader.ReadAsync(ct))
        {
            rows.Add(new Dictionary<string, object>
            {
                ["session_id"] = reader.GetString(0),
                ["provider_id"] = reader.GetString(1),
                ["model_id"] = reader.GetString(2),
                ["prompt_tokens"] = reader.GetInt64(3),
                ["completion_tokens"] = reader.GetInt64(4),
                ["tool_calls"] = reader.GetInt32(5),
                ["duration_ms"] = reader.GetInt64(6),
                ["estimated_cost"] = reader.GetDouble(7),
                ["project_path"] = reader.IsDBNull(8) ? "" : reader.GetString(8),
                ["trace_id"] = reader.IsDBNull(9) ? "" : reader.GetString(9),
                ["timestamp"] = reader.GetString(10),
            });
        }

        return format switch
        {
            UsageExportFormat.Json => JsonSerializer.Serialize(rows, s_jsonOptions),
            UsageExportFormat.Csv => FormatCsv(rows),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_db.State != System.Data.ConnectionState.Closed)
            await _db.CloseAsync();
        await _db.DisposeAsync();
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (!_initialized) await InitializeAsync(ct);
    }

    private async Task<UsageSummary> QueryUsageAsync(string whereClause, string? paramName, string? paramValue, CancellationToken ct)
    {
        await using var cmd = _db.CreateCommand();
#pragma warning disable CA2100 // whereClause is constructed internally from static strings
        cmd.CommandText = $"""
            SELECT provider_id,
                   SUM(prompt_tokens) as pt, SUM(completion_tokens) as ct_sum,
                   COUNT(*) as turns, SUM(tool_calls) as tc, SUM(estimated_cost) as cost
            FROM usage_turns
            {whereClause}
            GROUP BY provider_id
            """;
#pragma warning restore CA2100

        if (paramName is not null && paramValue is not null)
            cmd.Parameters.AddWithValue(paramName, paramValue);

        return await ReadSummaryAsync(cmd, ct);
    }

    private static async Task<UsageSummary> ReadSummaryAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var byProvider = new Dictionary<string, ProviderUsageBreakdown>(StringComparer.Ordinal);
        long totalPt = 0, totalCt = 0;
        int totalTurns = 0, totalTc = 0;
        decimal totalCost = 0m;

        while (await reader.ReadAsync(ct))
        {
            var pid = reader.GetString(0);
            var pt = reader.GetInt64(1);
            var ctVal = reader.GetInt64(2);
            var turns = reader.GetInt32(3);
            var tc = reader.GetInt32(4);
            var cost = (decimal)reader.GetDouble(5);

            byProvider[pid] = new ProviderUsageBreakdown
            {
                ProviderId = pid,
                PromptTokens = pt,
                CompletionTokens = ctVal,
                Turns = turns,
                EstimatedCostUsd = cost,
            };

            totalPt += pt;
            totalCt += ctVal;
            totalTurns += turns;
            totalTc += tc;
            totalCost += cost;
        }

        return new UsageSummary
        {
            TotalPromptTokens = totalPt,
            TotalCompletionTokens = totalCt,
            TotalTurns = totalTurns,
            TotalToolCalls = totalTc,
            EstimatedCostUsd = totalCost,
            ByProvider = byProvider,
        };
    }

    private static (DateTimeOffset From, DateTimeOffset To) GetPeriodRange(BudgetPeriod period)
    {
        var now = DateTimeOffset.UtcNow;
        var from = period switch
        {
            BudgetPeriod.Daily => new DateTimeOffset(now.Date, TimeSpan.Zero),
            BudgetPeriod.Weekly => new DateTimeOffset(now.AddDays(-(int)now.DayOfWeek).Date, TimeSpan.Zero),
            BudgetPeriod.Monthly => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
            BudgetPeriod.Session => DateTimeOffset.MinValue,
            _ => DateTimeOffset.MinValue,
        };
        return (from, now);
    }

    private static string FormatCsv(List<Dictionary<string, object>> rows)
    {
        if (rows.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', rows[0].Keys));

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', row.Values.Select(v =>
                v is string s && (s.Contains(',') || s.Contains('"'))
                    ? $"\"{s.Replace("\"", "\"\"")}\""
                    : v.ToString())));
        }

        return sb.ToString();
    }
}

/// <summary>Budget configuration with limit thresholds.</summary>
public sealed class BudgetConfig
{
    public decimal? DailyLimitUsd { get; set; }
    public decimal? WeeklyLimitUsd { get; set; }
    public decimal? MonthlyLimitUsd { get; set; }
    public decimal? SessionLimitUsd { get; set; }
    /// <summary>Percentage of budget at which to warn (0.0 - 1.0). Default: 80%.</summary>
    public decimal WarningThresholdPercent { get; set; } = 0.80m;
}
