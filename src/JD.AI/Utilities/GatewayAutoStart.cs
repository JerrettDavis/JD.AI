using System.Diagnostics;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Utilities;

/// <summary>
/// Auto-starts the gateway daemon in the background when the TUI launches.
/// The spawned process is non-persistent — it dies when the TUI exits.
/// </summary>
internal static class GatewayAutoStart
{
    private static Process? _daemonProcess;

    /// <summary>
    /// Starts jdai-daemon in background. Returns the process, or null if it couldn't start.
    /// The process is killed when the TUI exits.
    /// </summary>
    public static Process? StartBackground()
    {
        try
        {
            var psi = new ProcessStartInfo(DaemonServiceIdentity.ToolCommand, "run")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _daemonProcess = Process.Start(psi);

            // Register cleanup on process exit
            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopBackground();
            Console.CancelKeyPress += (_, _) => StopBackground();

            return _daemonProcess;
        }
#pragma warning disable CA1031
        catch
        {
            return null; // jdai-daemon not installed or not in PATH
        }
#pragma warning restore CA1031
    }

    public static void StopBackground()
    {
        try
        {
            if (_daemonProcess is { HasExited: false })
            {
                _daemonProcess.Kill(entireProcessTree: true);
                _daemonProcess.Dispose();
                _daemonProcess = null;
            }
        }
#pragma warning disable CA1031
        catch { /* best effort */ }
#pragma warning restore CA1031
    }
}
