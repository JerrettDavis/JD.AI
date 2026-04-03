using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JD.AI.Daemon.Services;

internal static class WindowsElevationHelper
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct LaunchResult(bool Started, int ExitCode);

    public static int? RelaunchAndWait(
        string arguments,
        Func<ProcessStartInfo, LaunchResult>? launchAndWait = null,
        TextWriter? output = null,
        string? exePath = null)
    {
        output ??= Console.Out;
        exePath ??= Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(exePath))
            return null;

        launchAndWait ??= StartAndWaitForExit;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
            };

            var result = launchAndWait(psi);
            if (!result.Started)
                return null;

            output.WriteLine("Relaunched with elevation prompt. Waiting for elevated process...");
            return result.ExitCode;
        }
        catch (Exception ex)
        {
            output.WriteLine($"Unable to relaunch elevated: {ex.Message}");
            return null;
        }
    }

    private static LaunchResult StartAndWaitForExit(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi);
        if (process is null)
            return new LaunchResult(false, 0);

        process.WaitForExit();
        return new LaunchResult(true, process.ExitCode);
    }
}
