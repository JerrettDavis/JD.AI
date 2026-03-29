using System.Diagnostics;

namespace JD.AI.Utilities;

/// <summary>
/// Cross-platform utility to open a URL in the default browser.
/// </summary>
internal static class BrowserLauncher
{
    public static void Open(string url)
    {
        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
        else if (OperatingSystem.IsLinux())
            Process.Start("xdg-open", url);
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", url);
    }
}
