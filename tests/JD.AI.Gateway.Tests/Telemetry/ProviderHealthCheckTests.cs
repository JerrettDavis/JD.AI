using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Telemetry.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Telemetry;

public sealed class ProviderHealthCheckTests
{
    private readonly IProviderRegistry _registry = Substitute.For<IProviderRegistry>();

    [Fact]
    public async Task CheckHealthAsync_WhenProvidersAvailable_ReturnsHealthy()
    {
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProviderInfo("ollama", IsAvailable: true, null, []),
                new ProviderInfo("claude-code", IsAvailable: true, null, []),
            ]);

        var check = new ProviderHealthCheck(_registry);
        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("2/2");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenNoProvidersAvailable_ReturnsDegraded()
    {
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProviderInfo("ollama", IsAvailable: false, "connection refused", []),
            ]);

        var check = new ProviderHealthCheck(_registry);
        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data.Should().ContainKey("availableCount");
        ((int)result.Data["availableCount"]).Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSomeProvidersAvailable_ReturnsHealthy()
    {
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([
                new ProviderInfo("ollama", IsAvailable: true, null, []),
                new ProviderInfo("claude-code", IsAvailable: false, "not installed", []),
            ]);

        var check = new ProviderHealthCheck(_registry);
        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("1/2");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDetectionThrows_ReturnsDegraded()
    {
        _registry.DetectProvidersAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ProviderInfo>>(_ => throw new InvalidOperationException("Network error"));

        var check = new ProviderHealthCheck(_registry);
        var result = await check.CheckHealthAsync(BuildContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    private static HealthCheckContext BuildContext() => new()
    {
        Registration = new HealthCheckRegistration(
            "providers",
            _ => Substitute.For<IHealthCheck>(),
            HealthStatus.Unhealthy,
            []),
    };
}
