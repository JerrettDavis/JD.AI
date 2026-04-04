using System.Diagnostics;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai gateway</c>.
/// By default this starts the full gateway persistently in the foreground by spawning
/// <c>jdai-daemon run</c> as a subprocess to avoid duplicating the gateway host setup.
/// The explicit <c>restart</c> action delegates to the installed <c>jdai-daemon</c> service.
/// </summary>
internal static class GatewayCliHandler
{
    internal readonly record struct DaemonCommandResult(bool Success, string Output);

    public static Task<int> RunAsync(string[] args)
    {
        return RunAsync(
            args,
            () => RunStartAsync(null),
            () => RunRestartAsync());
    }

    internal static async Task<int> RunAsync(
        string[] args,
        Func<Task<int>> runStartAsync,
        Func<Task<int>> runRestartAsync)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(runStartAsync);
        ArgumentNullException.ThrowIfNull(runRestartAsync);

        var action = NormalizeAction(args);

        return action switch
        {
            "start" => await runStartAsync().ConfigureAwait(false),
            "restart" => await runRestartAsync().ConfigureAwait(false),
            _ => WriteUsageError(action),
        };
    }

    internal static string NormalizeAction(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            return "start";

        return args[0].Trim().ToLowerInvariant();
    }

    internal static async Task<int> RunRestartAsync(Func<string, Task<DaemonCommandResult>>? daemonCommandRunner = null)
    {
        daemonCommandRunner ??= RunDaemonCommandAsync;

        Console.WriteLine("Restarting installed gateway service ...");

        var restartResult = await daemonCommandRunner("restart").ConfigureAwait(false);
        if (!restartResult.Success)
        {
            if (restartResult.Output.Contains("Service is not installed.", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("The installed gateway service is not available.");
                Console.Error.WriteLine("Use 'jdai gateway start' for the foreground gateway, or install the service before using 'jdai gateway restart'.");
            }

            if (!string.IsNullOrWhiteSpace(restartResult.Output))
                Console.Error.WriteLine(restartResult.Output.Trim());

            return 1;
        }

        Console.WriteLine("Installed gateway service restart completed.");
        return 0;
    }

    internal static async Task<int> RunStartAsync(
        string? baseUrl,
        Func<string?, Task<bool>>? isRunningAsync = null,
        Func<string?, int, Task<bool>>? waitForHealthyAsync = null,
        Func<IDaemonProcessHandle>? startDaemonProcess = null,
        Func<CancellationToken, Task>? waitForShutdownAsync = null)
    {
        isRunningAsync ??= url => GatewayHealthChecker.IsRunningAsync(url);
        waitForHealthyAsync ??= (url, maxWaitMs) => GatewayHealthChecker.WaitForHealthyAsync(url, maxWaitMs: maxWaitMs);
        startDaemonProcess ??= StartDaemonProcessCore;
        waitForShutdownAsync ??= WaitForShutdownAsync;
        var displayBaseUrl = baseUrl ?? GatewayHealthChecker.DefaultBaseUrl;

        // Check if gateway is already running
        if (await isRunningAsync(baseUrl).ConfigureAwait(false))
        {
            Console.WriteLine($"Gateway is already running at {displayBaseUrl}");
            return 0;
        }

        Console.WriteLine($"Starting gateway on {displayBaseUrl} ...");

        using var shutdownCts = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
        };

        Console.CancelKeyPress += handler;
        IDaemonProcessHandle? process = null;
        try
        {
            if (shutdownCts.IsCancellationRequested)
            {
                Console.WriteLine("Stopping gateway...");
                return 0;
            }

            // Spawn jdai-daemon run as a child process
            var daemonExe = DaemonServiceIdentity.ToolCommand;
            try
            {
                process = startDaemonProcess();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Failed to start daemon: {ex.Message}");
                Console.Error.WriteLine(
                    $"Ensure '{daemonExe}' is installed (dotnet tool install -g JD.AI.Daemon).");
                return 1;
            }

            // Wait for the health check to pass
            var healthyTask = waitForHealthyAsync(baseUrl, 15000);
            var exitTask = process.WaitForExitAsync();
            var shutdownTask = waitForShutdownAsync(shutdownCts.Token);
            var startupCompletedTask = await Task.WhenAny(healthyTask, exitTask, shutdownTask).ConfigureAwait(false);
            if (ReferenceEquals(startupCompletedTask, shutdownTask))
            {
                try
                {
                    await shutdownTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (shutdownCts.IsCancellationRequested)
                {
                    // Expected on Ctrl+C
                }

                Console.WriteLine("Stopping gateway...");
                return 0;
            }

            if (ReferenceEquals(startupCompletedTask, exitTask))
            {
                Console.Error.WriteLine("Gateway exited before becoming healthy.");
                return 1;
            }

            if (!await healthyTask.ConfigureAwait(false))
            {
                Console.Error.WriteLine("Gateway did not become healthy within 15 seconds.");
                return 1;
            }

            Console.WriteLine($"Gateway running at {displayBaseUrl}");
            Console.WriteLine("Press Ctrl+C to stop.");

            var completedTask = await Task.WhenAny(shutdownTask, exitTask).ConfigureAwait(false);
            if (ReferenceEquals(completedTask, exitTask) && !shutdownCts.IsCancellationRequested)
            {
                Console.Error.WriteLine("Gateway exited unexpectedly.");
                return 1;
            }

            try
            {
                await shutdownTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdownCts.IsCancellationRequested)
            {
                // Expected on Ctrl+C
            }

            Console.WriteLine("Stopping gateway...");
        }
        finally
        {
            try
            {
                if (process is not null)
                {
                    await CleanupDaemonProcessAsync(process).ConfigureAwait(false);
                }
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        Console.WriteLine("Gateway stopped.");
        return 0;
    }

    private static DaemonProcessHandle StartDaemonProcessCore()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = DaemonServiceIdentity.ToolCommand,
                Arguments = "run",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        return new DaemonProcessHandle(process);
    }

    private static async Task WaitForShutdownAsync(CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
    }

    private static async Task CleanupDaemonProcessAsync(IDaemonProcessHandle process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or ObjectDisposedException)
        {
            // Best-effort shutdown cleanup for the foreground daemon child.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<DaemonCommandResult> RunDaemonCommandAsync(string daemonArguments)
    {
        var daemonExe = DaemonServiceIdentity.ToolCommand;
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = daemonExe,
                    Arguments = daemonArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(
                process.WaitForExitAsync(),
                standardOutputTask,
                standardErrorTask).ConfigureAwait(false);

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            var output = string.IsNullOrWhiteSpace(standardError)
                ? standardOutput
                : string.IsNullOrWhiteSpace(standardOutput)
                    ? standardError
                    : $"{standardOutput}{Environment.NewLine}{standardError}";

            return new DaemonCommandResult(process.ExitCode == 0, output);
        }
        catch (Exception ex)
        {
            return new DaemonCommandResult(
                false,
                $"Failed to run daemon command '{daemonArguments}': {ex.Message}{Environment.NewLine}Ensure '{daemonExe}' is installed (dotnet tool install -g JD.AI.Daemon)."
            );
        }
    }

    private static int WriteUsageError(string action)
    {
        Console.Error.WriteLine($"Unknown gateway action '{action}'. Usage: jdai gateway [start|restart]");
        return 1;
    }

    internal interface IDaemonProcessHandle : IDisposable
    {
        bool HasExited { get; }
        void Kill();
        Task WaitForExitAsync();
    }

    private sealed class DaemonProcessHandle(Process process) : IDaemonProcessHandle
    {
        public bool HasExited => process.HasExited;

        public void Kill() => process.Kill(entireProcessTree: true);

        public Task WaitForExitAsync() => process.WaitForExitAsync();

        public void Dispose() => process.Dispose();
    }
}
