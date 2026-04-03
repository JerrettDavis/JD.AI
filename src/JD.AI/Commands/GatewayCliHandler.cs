using System.Diagnostics;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai gateway</c>.
/// By default this starts the full gateway persistently in the foreground by spawning
/// <c>jdai-daemon run</c> as a subprocess to avoid duplicating the gateway host setup.
/// The explicit <c>restart</c> action delegates to <c>jdai-daemon restart</c>.
/// </summary>
internal static class GatewayCliHandler
{
    internal readonly record struct DaemonCommandResult(bool Success, string Output);

    public static async Task<int> RunAsync(string[] args)
    {
        var action = NormalizeAction(args);
        var baseUrl = GatewayHealthChecker.DefaultBaseUrl;

        return action switch
        {
            "start" => await RunStartAsync(baseUrl).ConfigureAwait(false),
            "restart" => await RunRestartAsync(baseUrl).ConfigureAwait(false),
            _ => WriteUsageError(action),
        };
    }

    internal static string NormalizeAction(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            return "start";

        return args[0].Trim().ToLowerInvariant();
    }

    internal static async Task<int> RunRestartAsync(
        string baseUrl,
        Func<string, Task<DaemonCommandResult>>? daemonCommandRunner = null,
        Func<string, int, Task<bool>>? waitForHealthyAsync = null)
    {
        daemonCommandRunner ??= RunDaemonCommandAsync;
        waitForHealthyAsync ??= GatewayHealthChecker.WaitForHealthyAsync;

        Console.WriteLine($"Restarting gateway on {baseUrl} ...");

        var restartResult = await daemonCommandRunner("restart").ConfigureAwait(false);
        if (!restartResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(restartResult.Output))
                Console.Error.WriteLine(restartResult.Output.Trim());

            return 1;
        }

        if (!await waitForHealthyAsync(baseUrl, 15000).ConfigureAwait(false))
        {
            Console.Error.WriteLine("Gateway did not become healthy within 15 seconds after restart.");
            return 1;
        }

        Console.WriteLine($"Gateway running at {baseUrl}");
        return 0;
    }

    private static async Task<int> RunStartAsync(string baseUrl)
    {
        // Check if gateway is already running
        if (await GatewayHealthChecker.IsRunningAsync(baseUrl).ConfigureAwait(false))
        {
            Console.WriteLine($"Gateway is already running at {baseUrl}");
            return 0;
        }

        Console.WriteLine($"Starting gateway on {baseUrl} ...");

        // Spawn jdai-daemon run as a child process
        var daemonExe = DaemonServiceIdentity.ToolCommand;
        Process process;
        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = daemonExe,
                    Arguments = "run",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            process.Start();
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
        if (!await GatewayHealthChecker.WaitForHealthyAsync(baseUrl, maxWaitMs: 15000).ConfigureAwait(false))
        {
            Console.Error.WriteLine("Gateway did not become healthy within 15 seconds.");
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
            }

            return 1;
        }

        Console.WriteLine($"Gateway running at {baseUrl}");
        Console.WriteLine("Press Ctrl+C to stop.");

        // Block until Ctrl+C
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }

        Console.WriteLine("Stopping gateway...");
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }

        process.Dispose();
        Console.WriteLine("Gateway stopped.");
        return 0;
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
            await process.WaitForExitAsync().ConfigureAwait(false);

            var standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
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
}
