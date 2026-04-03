namespace JD.AI.Daemon.Services;

/// <summary>
/// Coordinates a safe service restart using the existing <see cref="IServiceManager"/> lifecycle surface.
/// </summary>
internal static class ServiceRestartCoordinator
{
    public static async Task<ServiceResult> RestartAsync(IServiceManager manager, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var status = await manager.GetStatusAsync(ct).ConfigureAwait(false);
        if (status.State == ServiceState.NotInstalled)
            return new ServiceResult(false, "Service is not installed.");

        if (status.State is ServiceState.Running or ServiceState.Starting or ServiceState.Stopping)
        {
            var stopResult = await manager.StopAsync(ct).ConfigureAwait(false);
            if (!stopResult.Success)
                return new ServiceResult(false, $"Failed to stop service before restart: {stopResult.Message}");
        }

        var startResult = await manager.StartAsync(ct).ConfigureAwait(false);
        if (!startResult.Success)
            return new ServiceResult(false, $"Failed to start service after restart: {startResult.Message}");

        return new ServiceResult(true, "Service restarted.");
    }
}
