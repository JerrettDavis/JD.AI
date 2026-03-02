using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Health check that verifies the gateway is operational.
/// </summary>
public sealed class GatewayHealthCheck : IHealthCheck
{
    private readonly AgentPoolService _pool;

    public GatewayHealthCheck(AgentPoolService pool) => _pool = pool;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var agents = _pool.ListAgents();
        var data = new Dictionary<string, object>
        {
            ["activeAgents"] = agents.Count,
            ["uptime"] = (DateTimeOffset.UtcNow - _startTime).ToString()
        };

        return Task.FromResult(HealthCheckResult.Healthy("Gateway operational", data));
    }

    private static readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
}
