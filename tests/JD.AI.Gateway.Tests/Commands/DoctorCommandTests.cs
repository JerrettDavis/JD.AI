using FluentAssertions;
using JD.AI.Core.Commands;
using JD.AI.Gateway.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Gateway.Tests.Commands;

public sealed class DoctorCommandTests
{
    private static CommandContext MakeContext() => new()
    {
        CommandName = "doctor",
        InvokerId = "user1",
        ChannelId = "ch1",
        ChannelType = "discord",
    };

    private static HealthCheckService BuildHealthService(
        params (string name, HealthReportEntry entry)[] entries)
    {
        // Build a real HealthCheckService backed by stub IHealthCheck implementations
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddHealthChecks();
        foreach (var (name, entry) in entries)
        {
            var status = entry.Status;
            var desc = entry.Description ?? "";
            builder.AddCheck(name, new DelegateHealthCheck(status, desc));
        }

        return services.BuildServiceProvider().GetRequiredService<HealthCheckService>();
    }

    [Fact]
    public void DoctorCommand_HasCorrectName()
    {
        var healthService = BuildHealthService();
        var cmd = new DoctorCommand(healthService);

        cmd.Name.Should().Be("doctor");
        cmd.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_HealthyReport_ContainsHealthyStatus()
    {
        var healthService = BuildHealthService(
            ("gateway", new HealthReportEntry(HealthStatus.Healthy, "Gateway operational", TimeSpan.Zero, null, null)));

        var cmd = new DoctorCommand(healthService);
        var result = await cmd.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("✔");
        result.Content.Should().Contain("Health:");
    }

    [Fact]
    public async Task ExecuteAsync_DegradedReport_ContainsDegradedStatus()
    {
        var healthService = BuildHealthService(
            ("providers", new HealthReportEntry(HealthStatus.Degraded, "No providers reachable", TimeSpan.Zero, null, null)));

        var cmd = new DoctorCommand(healthService);
        var result = await cmd.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("⚠");
    }

    [Fact]
    public async Task ExecuteAsync_UnhealthyReport_ContainsFailureIcon()
    {
        var healthService = BuildHealthService(
            ("session_store", new HealthReportEntry(HealthStatus.Unhealthy, "DB inaccessible", TimeSpan.Zero, null, null)));

        var cmd = new DoctorCommand(healthService);
        var result = await cmd.ExecuteAsync(MakeContext());

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("✘");
    }

    [Fact]
    public async Task ExecuteAsync_ReportContainsCheckNames()
    {
        var healthService = BuildHealthService(
            ("gateway", new HealthReportEntry(HealthStatus.Healthy, "OK", TimeSpan.Zero, null, null)),
            ("disk_space", new HealthReportEntry(HealthStatus.Healthy, "100GB free", TimeSpan.Zero, null, null)));

        var cmd = new DoctorCommand(healthService);
        var result = await cmd.ExecuteAsync(MakeContext());

        result.Content.Should().Contain("Gateway");
        result.Content.Should().Contain("Disk space");
    }

    /// <summary>Minimal health check that always returns a fixed status.</summary>
    private sealed class DelegateHealthCheck(HealthStatus status, string description) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var result = status switch
            {
                HealthStatus.Healthy => HealthCheckResult.Healthy(description),
                HealthStatus.Degraded => HealthCheckResult.Degraded(description),
                _ => HealthCheckResult.Unhealthy(description),
            };
            return Task.FromResult(result);
        }
    }
}
