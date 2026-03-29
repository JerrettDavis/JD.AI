using System.Diagnostics;

namespace JD.AI.Utilities;

/// <summary>
/// Cross-platform utility to open a URL in the default browser.
/// </summary>
internal static class BrowserLauncher
{
    public static void Open(string url)
    {
        // Don't actually open a browser if we're running in a test or non-interactive environment.
        if (IsTestEnvironment())
            return;

        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        else if (OperatingSystem.IsLinux())
            Process.Start("xdg-open", url);
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", url);
    }

    private static bool IsTestEnvironment()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VSTEST_HOSTING_PORT"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOREQ_RUNNING"))
            || Console.IsOutputRedirected;
    }
}
