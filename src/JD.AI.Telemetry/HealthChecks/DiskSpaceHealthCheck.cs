using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JD.AI.Telemetry.HealthChecks;

/// <summary>
/// Health check that verifies sufficient disk space is available in the JD.AI
/// data directory. Reports <see cref="HealthStatus.Degraded"/> when free space
/// drops below the configured threshold (default: 100 MB).
/// </summary>
public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly string _directory;
    private readonly long _minimumFreeBytes;

    /// <param name="directory">Directory to check (typically the JD.AI data root).</param>
    /// <param name="minimumFreeMegabytes">Minimum free space in MB before reporting Degraded.</param>
    public DiskSpaceHealthCheck(string directory, int minimumFreeMegabytes = 100)
    {
        _directory = directory;
        _minimumFreeBytes = (long)minimumFreeMegabytes * 1024 * 1024;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_directory) ?? _directory);
            var freeBytes = drive.AvailableFreeSpace;
            var freeGb = freeBytes / (1024.0 * 1024.0 * 1024.0);

            var data = new Dictionary<string, object>
            {
                ["freeSpaceBytes"] = freeBytes,
                ["freeSpaceGb"] = Math.Round(freeGb, 1),
                ["directory"] = _directory,
            };

            if (freeBytes < _minimumFreeBytes)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space: {freeGb:F1} GB free (minimum: {_minimumFreeBytes / 1024 / 1024} MB)",
                    data: data));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy($"{freeGb:F1} GB free", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Disk space check failed", ex));
        }
    }
}
