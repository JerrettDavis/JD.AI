using FluentAssertions;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Telemetry;

[Trait("Category", "FlakyEnvironment")]
public sealed class MemoryHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_BelowThreshold_ReturnsHealthy()
    {
        // 1 TB threshold ensures heap is always below
        var check = new MemoryHealthCheck(maximumMegabytes: 1024 * 1024);

        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("allocatedMb");
        result.Data.Should().ContainKey("allocatedBytes");
    }

    [Fact]
    public async Task CheckHealthAsync_AboveThreshold_ReturnsDegraded()
    {
        // 0 MB threshold means any allocation is too high
        var check = new MemoryHealthCheck(maximumMegabytes: 0);

        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("High memory usage");
    }

    [Fact]
    public async Task CheckHealthAsync_Data_ContainsAllocatedMb()
    {
        var check = new MemoryHealthCheck(maximumMegabytes: 1024 * 1024);

        var result = await check.CheckHealthAsync(BuildContext());

        var allocatedMb = (double)result.Data["allocatedMb"];
        allocatedMb.Should().BeGreaterThan(0);
    }

    private static HealthCheckContext BuildContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "memory",
            _ => Substitute.For<IHealthCheck>(),
            HealthStatus.Degraded,
            []),
    };
}
