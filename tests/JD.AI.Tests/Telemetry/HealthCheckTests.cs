using JD.AI.Core.Providers;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace JD.AI.Tests.Telemetry;

/// <summary>Tests for the three custom health checks: Disk, Memory, Provider, SessionStore.</summary>
public sealed class HealthCheckTests
{
    private static HealthCheckContext CreateContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "test",
            sp => Substitute.For<IHealthCheck>(),
            failureStatus: null,
            tags: null)
    };

    // ── DiskSpaceHealthCheck ──────────────────────────────────────────────────

    [Fact]
    public async Task DiskSpace_Healthy_WhenSufficientSpaceAvailable()
    {
        // Use a real temp directory — will have ample free space in any CI environment
        var check = new DiskSpaceHealthCheck(Path.GetTempPath(), minimumFreeMegabytes: 1);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("GB free", result.Description ?? string.Empty);
    }

    [Fact]
    public async Task DiskSpace_Degraded_WhenThresholdIsExtremelyHigh()
    {
        // Require 1 TB free — will trigger Degraded on any normal machine
        var check = new DiskSpaceHealthCheck(Path.GetTempPath(), minimumFreeMegabytes: 1024 * 1024);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Low disk space", result.Description ?? string.Empty);
    }

    [Fact]
    public async Task DiskSpace_DataContainsExpectedKeys()
    {
        var check = new DiskSpaceHealthCheck(Path.GetTempPath(), minimumFreeMegabytes: 1);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.True(result.Data.ContainsKey("freeSpaceBytes"));
        Assert.True(result.Data.ContainsKey("freeSpaceGb"));
        Assert.True(result.Data.ContainsKey("directory"));
    }

    [Fact]
    public async Task DiskSpace_CancelledToken_DoesNotThrow()
    {
        var check = new DiskSpaceHealthCheck(Path.GetTempPath(), minimumFreeMegabytes: 1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Synchronous check — cancellation has no effect; should return a valid status
        var result = await check.CheckHealthAsync(CreateContext(), cts.Token);
        Assert.True(Enum.IsDefined(result.Status));
    }

    // ── MemoryHealthCheck ─────────────────────────────────────────────────────

    [Fact]
    public async Task Memory_Healthy_WhenBelowThreshold()
    {
        // Require 1 TB max — way above current managed heap
        var check = new MemoryHealthCheck(maximumMegabytes: 1024 * 1024);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("MB managed heap", result.Description ?? string.Empty);
    }

    [Fact]
    public async Task Memory_Degraded_WhenAboveThreshold()
    {
        // Require 0 MB max — always exceeded
        var check = new MemoryHealthCheck(maximumMegabytes: 0);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("High memory usage", result.Description ?? string.Empty);
    }

    [Fact]
    public async Task Memory_DataContainsExpectedKeys()
    {
        var check = new MemoryHealthCheck();
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.True(result.Data.ContainsKey("allocatedBytes"));
        Assert.True(result.Data.ContainsKey("allocatedMb"));
    }

    [Fact]
    public async Task Memory_AllocatedBytes_IsPositive()
    {
        var check = new MemoryHealthCheck();
        var result = await check.CheckHealthAsync(CreateContext());

        var bytes = (long)result.Data["allocatedBytes"];
        Assert.True(bytes > 0);
    }

    // ── ProviderHealthCheck ───────────────────────────────────────────────────

    private static ProviderInfo MakeProvider(string name, bool available) =>
        new(name, available, null, []);

    [Fact]
    public async Task Provider_Healthy_WhenAtLeastOneAvailable()
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                MakeProvider("Claude", available: true),
                MakeProvider("OpenAI", available: false),
            ]));

        var check = new ProviderHealthCheck(registry);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("1/2", result.Description ?? string.Empty);
    }

    [Fact]
    public async Task Provider_Degraded_WhenNoneAvailable()
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                MakeProvider("Claude", available: false),
                MakeProvider("OpenAI", available: false),
            ]));

        var check = new ProviderHealthCheck(registry);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("No providers reachable", result.Description ?? string.Empty);
    }

    [Fact]
    public async Task Provider_Degraded_WhenRegistryThrows()
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<ProviderInfo>>>(_ =>
                Task.FromException<IReadOnlyList<ProviderInfo>>(new InvalidOperationException("timeout")));

        var check = new ProviderHealthCheck(registry);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Provider detection failed", result.Description ?? string.Empty);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Provider_DataContainsExpectedKeys()
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                MakeProvider("Claude", available: true),
            ]));

        var check = new ProviderHealthCheck(registry);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.True(result.Data.ContainsKey("available"));
        Assert.True(result.Data.ContainsKey("unavailable"));
        Assert.True(result.Data.ContainsKey("availableCount"));
        Assert.True(result.Data.ContainsKey("totalCount"));
    }

    [Fact]
    public async Task Provider_Healthy_WhenAllAvailable()
    {
        var registry = Substitute.For<IProviderRegistry>();
        registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ProviderInfo>>(
            [
                MakeProvider("Claude", available: true),
                MakeProvider("OpenAI", available: true),
                MakeProvider("Ollama", available: true),
            ]));

        var check = new ProviderHealthCheck(registry);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("3/3", result.Description ?? string.Empty);
    }

    // ── SessionStoreHealthCheck ───────────────────────────────────────────────

    [Fact]
    public async Task SessionStore_Unhealthy_WhenFileAbsent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db");
        var check = new SessionStoreHealthCheck(dbPath);
        var result = await check.CheckHealthAsync(CreateContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task SessionStore_Unhealthy_WhenSessionsTableAbsent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"nosessions-{Guid.NewGuid():N}.db");
        try
        {
            // Create a DB with a different table (no 'sessions' table)
            var cs = $"Data Source={dbPath}";
            await using (var conn = new SqliteConnection(cs))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE other (id INTEGER PRIMARY KEY)";
                await cmd.ExecuteNonQueryAsync();
            }

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(CreateContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("'sessions' table not found", result.Description ?? string.Empty);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SessionStore_Healthy_WhenSessionsTableExists()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}.db");
        try
        {
            var cs = $"Data Source={dbPath}";
            await using (var conn = new SqliteConnection(cs))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE sessions (id TEXT PRIMARY KEY, data TEXT)";
                await cmd.ExecuteNonQueryAsync();
            }

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(CreateContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("SQLite OK", result.Description ?? string.Empty);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SessionStore_Healthy_ReportsSessionCount()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sessions2-{Guid.NewGuid():N}.db");
        try
        {
            var cs = $"Data Source={dbPath}";
            await using (var conn = new SqliteConnection(cs))
            {
                await conn.OpenAsync();
                await using var setupCmd = conn.CreateCommand();
                setupCmd.CommandText = """
                    CREATE TABLE sessions (id TEXT PRIMARY KEY, data TEXT);
                    INSERT INTO sessions VALUES ('s1', '{}');
                    INSERT INTO sessions VALUES ('s2', '{}');
                    """;
                await setupCmd.ExecuteNonQueryAsync();
            }

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(CreateContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Contains("2 sessions", result.Description ?? string.Empty);
            Assert.Equal(2L, result.Data["sessionCount"]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SessionStore_DataContainsDbPath()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sessions3-{Guid.NewGuid():N}.db");
        try
        {
            var cs = $"Data Source={dbPath}";
            await using (var conn = new SqliteConnection(cs))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE sessions (id TEXT PRIMARY KEY)";
                await cmd.ExecuteNonQueryAsync();
            }

            var check = new SessionStoreHealthCheck(dbPath);
            var result = await check.CheckHealthAsync(CreateContext());

            Assert.True(result.Data.ContainsKey("dbPath"));
            Assert.Equal(dbPath, result.Data["dbPath"]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }
}
