using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Telemetry.HealthChecks;

/// <summary>
/// Health check that verifies the SQLite session store is accessible.
/// Reports <see cref="HealthStatus.Unhealthy"/> when the database cannot be
/// opened or queried, since a broken session store prevents conversation
/// persistence.
/// </summary>
public sealed class SessionStoreHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public SessionStoreHealthCheck(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='sessions'";

            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var tableExists = result is long count && count > 0;

            if (!tableExists)
            {
                return HealthCheckResult.Unhealthy(
                    "Session store: 'sessions' table not found — has InitializeAsync been called?");
            }

            // Count sessions for informational data
            cmd.CommandText = "SELECT COUNT(*) FROM sessions";
            var sessionCount = (long)(await cmd.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false) ?? 0L);

            var data = new Dictionary<string, object>
            {
                ["sessionCount"] = sessionCount,
                ["dbPath"] = _connectionString.Replace("Data Source=", "", StringComparison.Ordinal),
            };

            return HealthCheckResult.Healthy($"SQLite OK ({sessionCount} sessions)", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Session store inaccessible", ex);
        }
    }
}
