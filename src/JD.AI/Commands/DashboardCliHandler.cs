using System.Diagnostics;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Commands;

/// <summary>
/// Handles <c>jdai dashboard</c> — ensures the gateway is running, then opens the dashboard in a browser.
/// </summary>
internal static class DashboardCliHandler
{
    internal static Func<Task<string?>> GetRunningGatewayBaseUrlAsync { get; set; } =
        () => GatewayHealthChecker.GetReachableBaseUrlAsync();

    internal static Func<int, Task<string?>> WaitForGatewayHealthyBaseUrlAsync { get; set; } =
        maxWaitMs => GatewayHealthChecker.WaitForHealthyBaseUrlAsync(maxWaitMs: maxWaitMs);

    internal static Func<IDaemonProcessHandle> StartDaemonProcess { get; set; } = StartDaemonProcessCore;

    internal static Action<string> OpenBrowser { get; set; } = BrowserLauncher.Open;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            Console.Error.WriteLine("Usage: jdai dashboard");
            return 1;
        }

        var baseUrl = await GetRunningGatewayBaseUrlAsync().ConfigureAwait(false);

        // Check if gateway is already running
        if (baseUrl is not null)
        {
            Console.WriteLine($"Gateway is running at {baseUrl}");
            return TryOpenDashboard(baseUrl);
        }

        // Start gateway in background
        Console.WriteLine("Gateway not running. Starting in background...");
        IDaemonProcessHandle process;
        try
        {
            process = StartDaemonProcess();
        }
        catch (Exception ex)
        {
            var daemonExe = DaemonServiceIdentity.ToolCommand;
            Console.Error.WriteLine(
                $"Failed to start daemon: {ex.Message}");
            Console.Error.WriteLine(
                $"Ensure '{daemonExe}' is installed (dotnet tool install -g JD.AI.Daemon).");
            return 1;
        }

        try
        {
            Console.WriteLine("Waiting for gateway to become healthy...");
            baseUrl = await WaitForGatewayHealthyBaseUrlAsync(15000).ConfigureAwait(false);
            if (baseUrl is null)
            {
                Console.Error.WriteLine("Gateway did not become healthy within 15 seconds.");
                if (!process.HasExited)
                    process.Kill();

                return 1;
            }

            Console.WriteLine($"Gateway running at {baseUrl}");
            return TryOpenDashboard(baseUrl);
        }
        finally
        {
            process.Dispose();
        }
    }

    internal interface IDaemonProcessHandle : IDisposable
    {
        bool HasExited { get; }
        void Kill();
    }

    private sealed class DaemonProcessHandle(Process process) : IDaemonProcessHandle
    {
        public bool HasExited => process.HasExited;

        public void Kill() => process.Kill(entireProcessTree: true);

        public void Dispose() => process.Dispose();
    }

    private static IDaemonProcessHandle StartDaemonProcessCore()
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

    private static int TryOpenDashboard(string baseUrl)
    {
        try
        {
            OpenBrowser($"{baseUrl}/");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open dashboard automatically: {ex.Message}");
            Console.Error.WriteLine($"Open this URL manually: {baseUrl}/");
            return 1;
        }
    }
}
