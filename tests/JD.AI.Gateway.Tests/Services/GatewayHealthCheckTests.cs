using FluentAssertions;
using JD.AI.Core.Channels;
using JD.AI.Core.Events;
using JD.AI.Core.Providers;
using JD.AI.Gateway.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Gateway.Tests.Services;

public sealed class GatewayHealthCheckTests
{
    private static AgentPoolService CreatePool()
    {
        var providers = Substitute.For<IProviderRegistry>();
        var events = Substitute.For<IEventBus>();
        var logger = NullLogger<AgentPoolService>.Instance;
        return new AgentPoolService(providers, new ChannelRegistry(), events, logger);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy()
    {
        var pool = CreatePool();
        var check = new GatewayHealthCheck(pool);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Gateway operational");
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesActiveAgentCount()
    {
        var pool = CreatePool();
        var check = new GatewayHealthCheck(pool);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Data.Should().ContainKey("activeAgents");
        result.Data["activeAgents"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesUptime()
    {
        var pool = CreatePool();
        var check = new GatewayHealthCheck(pool);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Data.Should().ContainKey("uptime");
        result.Data["uptime"].Should().NotBeNull();
    }
}
