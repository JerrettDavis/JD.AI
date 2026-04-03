using System.Diagnostics;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Coordinates a safe service restart using the existing <see cref="IServiceManager"/> lifecycle surface.
/// </summary>
internal static class ServiceRestartCoordinator
{
    public static async Task<ServiceResult> RestartAsync(
        IServiceManager manager,
        TimeSpan? transitionTimeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var timeout = transitionTimeout ?? TimeSpan.FromSeconds(15);
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(250);

        var status = await manager.GetStatusAsync(ct).ConfigureAwait(false);
        if (status.State == ServiceState.NotInstalled)
            return new ServiceResult(false, "Service is not installed.");

        if (status.State is ServiceState.Running or ServiceState.Starting)
        {
            var stopResult = await manager.StopAsync(ct).ConfigureAwait(false);
            if (!stopResult.Success)
                return new ServiceResult(false, $"Failed to stop service before restart: {stopResult.Message}");

            var stopWaitResult = await WaitForStateAsync(manager, ServiceState.Stopped, timeout, interval, ct).ConfigureAwait(false);
            if (!stopWaitResult.Success)
                return stopWaitResult;
        }
        else if (status.State == ServiceState.Stopping)
        {
            var stopWaitResult = await WaitForStateAsync(manager, ServiceState.Stopped, timeout, interval, ct).ConfigureAwait(false);
            if (!stopWaitResult.Success)
                return stopWaitResult;
        }

        var startResult = await manager.StartAsync(ct).ConfigureAwait(false);
        if (!startResult.Success)
            return new ServiceResult(false, $"Failed to start service after restart: {startResult.Message}");

        var startWaitResult = await WaitForStateAsync(manager, ServiceState.Running, timeout, interval, ct).ConfigureAwait(false);
        if (!startWaitResult.Success)
            return startWaitResult;

        return new ServiceResult(true, "Service restarted.");
    }

    private static async Task<ServiceResult> WaitForStateAsync(
        IServiceManager manager,
        ServiceState targetState,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var status = await manager.GetStatusAsync(ct).ConfigureAwait(false);
            if (status.State == targetState)
                return new ServiceResult(true, $"Service reached '{targetState}'.");

            if (stopwatch.Elapsed >= timeout)
                return new ServiceResult(false, $"Service did not reach '{targetState}' within {timeout.TotalMilliseconds:0} ms during restart.");

            await Task.Delay(pollInterval, ct).ConfigureAwait(false);
        }
    }
}
