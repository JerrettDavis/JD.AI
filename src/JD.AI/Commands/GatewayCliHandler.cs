using System.Diagnostics;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai gateway</c> — starts the full gateway persistently.
/// Spawns <c>jdai-daemon run</c> as a subprocess to avoid duplicating the gateway host setup.
/// </summary>
internal static class GatewayCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = GatewayHealthChecker.DefaultBaseUrl;

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
}
