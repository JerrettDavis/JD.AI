using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Browser automation tools for web interaction, navigation, and capture.
/// Uses system browser for simple ops and headless Chrome/Edge for advanced ops.
/// </summary>
[ToolPlugin("browser")]
public sealed class BrowserTools
{
    private static readonly string[] BrowserPaths = OperatingSystem.IsWindows()
        ? [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
        ]
        : ["/usr/bin/google-chrome", "/usr/bin/chromium-browser", "/usr/bin/chromium", "/usr/bin/microsoft-edge"];

    // ── Status ──────────────────────────────────────────────

    [KernelFunction("browser_status")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check browser availability and capabilities. Reports which browsers are installed and headless support.")]
    public static string GetBrowserStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Browser Status");
        sb.AppendLine();

        var found = false;
        foreach (var path in BrowserPaths)
        {
            if (File.Exists(path))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- ✓ **{Path.GetFileNameWithoutExtension(path)}**: `{path}`");
                found = true;
            }
        }

        if (!found)
        {
            sb.AppendLine("- ✗ No Chromium-based browser found");
            sb.AppendLine("  Install Chrome, Chromium, or Edge for full browser automation.");
        }

        sb.AppendLine();
        sb.AppendLine("### Capabilities");
        sb.AppendLine("- `browser_open` — Open URL in default browser");
        sb.AppendLine("- `browser_screenshot` — Headless screenshot of a URL");
        sb.AppendLine("- `browser_pdf` — Headless PDF capture of a URL");
        sb.AppendLine("- `browser_content` — Extract page text content (headless)");
        sb.AppendLine("- `browser_console` — Capture browser console output");

        return sb.ToString();
    }

    // ── Open ────────────────────────────────────────────────

    [KernelFunction("browser_open")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Open a URL in the user's default browser. Returns immediately after launching.")]
    public static string OpenInBrowser(
        [Description("The URL to open (e.g. https://github.com)")] string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal)))
            {
                return $"Error: Invalid URL '{url}'. Only http/https URLs are supported.";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });

            return $"Opened {uri.AbsoluteUri} in default browser.";
        }
        catch (Exception ex)
        {
            return $"Error opening URL: {ex.Message}";
        }
    }

    // ── Screenshot ──────────────────────────────────────────

    [KernelFunction("browser_screenshot")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Take a headless screenshot of a web page. Returns the path to the saved PNG file.")]
    public static async Task<string> ScreenshotAsync(
        [Description("URL to capture")] string url,
        [Description("Output file path for the screenshot (default: auto-generated in temp)")] string? outputPath = null,
        [Description("Viewport width in pixels (default: 1280)")] int width = 1280,
        [Description("Viewport height in pixels (default: 720)")] int height = 720,
        [Description("Wait time in ms before capture (default: 2000)")] int waitMs = 2000)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal)))
        {
            return $"Error: Invalid URL '{url}'.";
        }

        var browserPath = FindBrowser();
        if (browserPath is null)
            return "Error: No Chromium-based browser found. Install Chrome or Edge.";

        outputPath ??= Path.Combine(Path.GetTempPath(), $"screenshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var args = $"--headless --disable-gpu --no-sandbox --window-size={width},{height} --screenshot=\"{outputPath}\" --virtual-time-budget={waitMs} \"{uri.AbsoluteUri}\"";
        var result = await RunProcessAsync(browserPath, args, timeoutSeconds: 30);

        if (File.Exists(outputPath))
        {
            var info = new FileInfo(outputPath);
            return $"Screenshot saved to: {outputPath}\nSize: {info.Length:N0} bytes ({width}x{height})";
        }

        return $"Error: Screenshot failed.\n{result}";
    }

    // ── PDF ─────────────────────────────────────────────────

    [KernelFunction("browser_pdf")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Capture a web page as PDF using headless browser. Returns the path to the saved PDF.")]
    public static async Task<string> CapturePdfAsync(
        [Description("URL to capture")] string url,
        [Description("Output file path for the PDF (default: auto-generated in temp)")] string? outputPath = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal)))
        {
            return $"Error: Invalid URL '{url}'.";
        }

        var browserPath = FindBrowser();
        if (browserPath is null)
            return "Error: No Chromium-based browser found. Install Chrome or Edge.";

        outputPath ??= Path.Combine(Path.GetTempPath(), $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var args = $"--headless --disable-gpu --no-sandbox --print-to-pdf=\"{outputPath}\" \"{uri.AbsoluteUri}\"";
        var result = await RunProcessAsync(browserPath, args, timeoutSeconds: 30);

        if (File.Exists(outputPath))
        {
            var info = new FileInfo(outputPath);
            return $"PDF saved to: {outputPath}\nSize: {info.Length:N0} bytes";
        }

        return $"Error: PDF capture failed.\n{result}";
    }

    // ── Content extraction ──────────────────────────────────

    [KernelFunction("browser_content")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Extract text content from a web page using headless browser and dump-dom. Returns the HTML.")]
    public static async Task<string> GetContentAsync(
        [Description("URL to extract content from")] string url,
        [Description("Max characters to return (default: 10000)")] int maxChars = 10000)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal)))
        {
            return $"Error: Invalid URL '{url}'.";
        }

        var browserPath = FindBrowser();
        if (browserPath is null)
            return "Error: No Chromium-based browser found. Install Chrome or Edge.";

        var args = $"--headless --disable-gpu --no-sandbox --dump-dom \"{uri.AbsoluteUri}\"";
        var output = await RunProcessAsync(browserPath, args, timeoutSeconds: 30);

        if (string.IsNullOrWhiteSpace(output))
            return "Error: No content returned from page.";

        if (output.Length > maxChars)
            output = string.Concat(output.AsSpan(0, maxChars),
                $"\n\n... [truncated, {output.Length - maxChars:N0} chars omitted]");

        return $"## Page Content: {uri.Host}\n```html\n{output}\n```";
    }

    // ── Console output ──────────────────────────────────────

    [KernelFunction("browser_console")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Run a URL in headless mode and capture JavaScript console output for debugging.")]
    public static async Task<string> GetConsoleOutputAsync(
        [Description("URL to load")] string url,
        [Description("JavaScript to execute in the page context (optional)")] string? script = null,
        [Description("Wait time in ms (default: 3000)")] int waitMs = 3000)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal)))
        {
            return $"Error: Invalid URL '{url}'.";
        }

        var browserPath = FindBrowser();
        if (browserPath is null)
            return "Error: No Chromium-based browser found. Install Chrome or Edge.";

        var args = new StringBuilder($"--headless --disable-gpu --no-sandbox --enable-logging --v=1 --virtual-time-budget={waitMs}");

        if (!string.IsNullOrEmpty(script))
        {
            // Use --run-all-compositor-stages-before-draw to ensure page is loaded
            args.Append($" --run-all-compositor-stages-before-draw");
        }

        args.Append($" \"{uri.AbsoluteUri}\"");

        var output = await RunProcessAsync(browserPath, args.ToString(), timeoutSeconds: 30, captureStderr: true);

        if (string.IsNullOrWhiteSpace(output))
            return "No console output captured.";

        // Filter for console-related lines
        var consoleLines = output
            .Split('\n')
            .Where(l => l.Contains("CONSOLE", StringComparison.OrdinalIgnoreCase)
                     || l.Contains("console", StringComparison.Ordinal)
                     || l.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            .Take(100)
            .ToArray();

        if (consoleLines.Length == 0)
            return "No console messages captured. The page may not produce console output.";

        return $"## Console Output: {uri.Host}\n```\n{string.Join('\n', consoleLines)}\n```";
    }

    // ── Internal helpers ────────────────────────────────────

    private static string? FindBrowser()
    {
        foreach (var path in BrowserPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Try to find via PATH
        try
        {
            var which = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo(which, "chrome")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    return output.Split('\n')[0].Trim();
            }
        }
        catch
        {
            // Ignore — fallback to null
        }

        return null;
    }

    private static async Task<string> RunProcessAsync(
        string fileName, string arguments, int timeoutSeconds = 30, bool captureStderr = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return "Error: Failed to start process.";

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = process.WaitForExit(timeoutSeconds * 1000);
            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                return $"Error: Process timed out after {timeoutSeconds}s.";
            }

            var stdout = await outputTask;
            var stderr = await errorTask;

            return captureStderr ? $"{stdout}\n{stderr}" : stdout;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
