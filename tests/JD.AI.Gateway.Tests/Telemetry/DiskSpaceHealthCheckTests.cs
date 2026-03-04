using FluentAssertions;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Telemetry;

public sealed class DiskSpaceHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WithSufficientSpace_ReturnsHealthy()
    {
        // Use 1 MB minimum to always pass on any machine
        var check = new DiskSpaceHealthCheck(Path.GetTempPath(), minimumFreeMegabytes: 1);

        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("freeSpaceGb");
        result.Data.Should().ContainKey("freeSpaceBytes");
    }

    [Fact]
    public async Task CheckHealthAsync_WithExcessiveMinimum_ReturnsDegraded()
    {
        // Derive a threshold guaranteed to exceed available free space on this drive,
        // using the same DriveInfo lookup as the production DiskSpaceHealthCheck.
        var tempDir = Path.GetTempPath();
        var drive = new DriveInfo(Path.GetPathRoot(tempDir) ?? tempDir);
        var currentFreeMb = (int)(drive.AvailableFreeSpace / (1024 * 1024));
        var check = new DiskSpaceHealthCheck(tempDir, minimumFreeMegabytes: currentFreeMb + 1);

        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Low disk space");
    }

    [Fact]
    public async Task CheckHealthAsync_Data_ContainsDirectory()
    {
        var dir = Path.GetTempPath();
        var check = new DiskSpaceHealthCheck(dir, minimumFreeMegabytes: 1);

        var result = await check.CheckHealthAsync(BuildContext());

        result.Data.Should().ContainKey("directory");
        result.Data["directory"].Should().Be(dir);
    }

    private static HealthCheckContext BuildContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "disk_space",
            _ => Substitute.For<IHealthCheck>(),
            HealthStatus.Degraded,
            []),
    };
}
