using JD.AI.Core.Providers;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Telemetry.HealthChecks;

/// <summary>
/// Health check that verifies at least one AI provider is reachable.
/// Reports <see cref="HealthStatus.Degraded"/> when no providers are available
/// (the gateway can still run, but cannot serve agent requests).
/// </summary>
public sealed class ProviderHealthCheck : IHealthCheck
{
    private readonly IProviderRegistry _registry;

    public ProviderHealthCheck(IProviderRegistry registry) => _registry = registry;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providers = await _registry.DetectProvidersAsync(cancellationToken)
                .ConfigureAwait(false);

            var available = providers.Where(p => p.IsAvailable).ToList();
            var unavailable = providers.Where(p => !p.IsAvailable).ToList();

            var data = new Dictionary<string, object>
            {
                ["available"] = available.Select(p => p.Name).ToArray(),
                ["unavailable"] = unavailable.Select(p => p.Name).ToArray(),
                ["availableCount"] = available.Count,
                ["totalCount"] = providers.Count,
            };

            if (available.Count == 0)
            {
                return HealthCheckResult.Degraded(
                    $"No providers reachable (checked {providers.Count})", data: data);
            }

            return HealthCheckResult.Healthy(
                $"{available.Count}/{providers.Count} providers reachable", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Provider detection failed", ex);
        }
    }
}
