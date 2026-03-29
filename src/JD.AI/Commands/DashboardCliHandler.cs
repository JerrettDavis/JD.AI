using System.Diagnostics;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai dashboard</c> — ensures the gateway is running, then opens the dashboard in a browser.
/// </summary>
internal static class DashboardCliHandler
{
    public static async Task<int> RunAsync(string[] args)
    {
        var baseUrl = GatewayHealthChecker.DefaultBaseUrl;

        // Check if gateway is already running
        if (await GatewayHealthChecker.IsRunningAsync(baseUrl).ConfigureAwait(false))
        {
            Console.WriteLine($"Gateway is running at {baseUrl}");
            BrowserLauncher.Open($"{baseUrl}/");
            return 0;
        }

        // Start gateway in background
        Console.WriteLine("Gateway not running. Starting in background...");
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

        // Wait for health check
        Console.WriteLine("Waiting for gateway to become healthy...");
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
        BrowserLauncher.Open($"{baseUrl}/");

        // Detach — the daemon continues running in the background
        process.Dispose();
        return 0;
    }
}
