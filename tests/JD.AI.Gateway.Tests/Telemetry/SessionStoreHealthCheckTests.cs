using FluentAssertions;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Telemetry;

public sealed class SessionStoreHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenDbExists_ReturnsHealthy()
    {
        // Use a temp directory for the test database
        var dbPath = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}.db");
        try
        {
            // Initialize DB with the sessions table
            await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE sessions (id TEXT PRIMARY KEY)";
            await cmd.ExecuteNonQueryAsync();

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(BuildContext());

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("SQLite OK");
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTableMissing_ReturnsUnhealthy()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}.db");
        try
        {
            // Create an empty database (no tables)
            await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            await conn.OpenAsync();

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(BuildContext());

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain("sessions");
        }
        finally
        {
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
