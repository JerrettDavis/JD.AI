using FluentAssertions;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Telemetry;

public sealed class SessionStoreHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDbExists_ReturnsHealthy()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}.db");
        try
        {
            // Initialize DB with the sessions table, then close before health check
            var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE sessions (id TEXT PRIMARY KEY)";
            await cmd.ExecuteNonQueryAsync();
            await cmd.DisposeAsync();
            await conn.DisposeAsync();

            // Clear SQLite connection pool so the file is fully released
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(BuildContext());

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("SQLite OK");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTableMissing_ReturnsUnhealthy()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}.db");
        try
        {
            // Create an empty database (no tables), then close
            var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            await conn.OpenAsync();
            await conn.DisposeAsync();

            // Clear SQLite connection pool so the file is fully released
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(BuildContext());

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain("sessions");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    private static HealthCheckContext BuildContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "session_store",
            _ => Substitute.For<IHealthCheck>(),
            HealthStatus.Unhealthy,
            []),
    };
}
