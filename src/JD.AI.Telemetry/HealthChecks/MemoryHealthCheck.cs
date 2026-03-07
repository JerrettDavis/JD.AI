using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Telemetry.HealthChecks;

/// <summary>
/// Health check that monitors managed heap memory usage.
/// Reports <see cref="HealthStatus.Degraded"/> when the managed heap exceeds
/// the configured threshold (default: 1 GB).
/// </summary>
public sealed class MemoryHealthCheck : IHealthCheck
{
    private readonly long _maximumBytes;

    /// <param name="maximumMegabytes">Maximum managed heap size in MB before reporting Degraded.</param>
    public MemoryHealthCheck(int maximumMegabytes = 1024)
    {
        _maximumBytes = (long)maximumMegabytes * 1024 * 1024;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocatedBytes = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedMb = allocatedBytes / (1024.0 * 1024.0);

        var data = new Dictionary<string, object>
        {
            ["allocatedBytes"] = allocatedBytes,
            ["allocatedMb"] = Math.Round(allocatedMb, 1),
        };

        if (allocatedBytes > _maximumBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"High memory usage: {allocatedMb:F0} MB managed heap (maximum: {_maximumBytes / 1024 / 1024} MB)",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"{allocatedMb:F0} MB managed heap", data));
    }
}
