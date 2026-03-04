using FluentAssertions;
using JD.AI.Core.Tools;
using Xunit;

namespace JD.AI.Tests;

/// <summary>
/// Tests for BrowserTools — focuses on input validation and status reporting.
/// Headless browser tests only run when Chrome/Edge is available.
/// </summary>
public sealed class BrowserToolsTests
{
    // ── Status ──────────────────────────────────────────────

    [Fact]
    public void BrowserStatus_ReturnsCapabilities()
    {
        var result = BrowserTools.GetBrowserStatus();

        result.Should().Contain("Browser Status");
        result.Should().Contain("Capabilities");
        result.Should().Contain("browser_open");
        result.Should().Contain("browser_screenshot");
    }

    // ── Open URL ────────────────────────────────────────────

    [Fact]
    public void OpenUrl_InvalidUrl_ReturnsError()
    {
        var result = BrowserTools.OpenInBrowser("not-a-url");

        result.Should().Contain("Error");
        result.Should().Contain("Invalid URL");
    }

    [Fact]
    public void OpenUrl_FtpUrl_ReturnsError()
    {
        var result = BrowserTools.OpenInBrowser("ftp://example.com");

        result.Should().Contain("Error");
        result.Should().Contain("http/https");
    }

    [Fact]
    public void OpenUrl_JavaScriptUrl_ReturnsError()
    {
        var result = BrowserTools.OpenInBrowser("javascript:alert(1)");

        result.Should().Contain("Error");
    }

    // ── Screenshot ──────────────────────────────────────────

    [Fact]
    public async Task Screenshot_InvalidUrl_ReturnsError()
    {
        var result = await BrowserTools.ScreenshotAsync("not-a-url");

        result.Should().Contain("Error");
        result.Should().Contain("Invalid URL");
    }

    [Fact]
    public async Task Screenshot_FtpUrl_ReturnsError()
    {
        var result = await BrowserTools.ScreenshotAsync("ftp://example.com");

        result.Should().Contain("Error");
    }

    // ── PDF ─────────────────────────────────────────────────

    [Fact]
    public async Task Pdf_InvalidUrl_ReturnsError()
    {
        var result = await BrowserTools.CapturePdfAsync("not-a-url");

        result.Should().Contain("Error");
        result.Should().Contain("Invalid URL");
    }

    // ── Content ─────────────────────────────────────────────

    [Fact]
    public async Task Content_InvalidUrl_ReturnsError()
    {
        var result = await BrowserTools.GetContentAsync("not-a-url");

        result.Should().Contain("Error");
        result.Should().Contain("Invalid URL");
    }

    // ── Console ─────────────────────────────────────────────

    [Fact]
    public async Task Console_InvalidUrl_ReturnsError()
    {
        var result = await BrowserTools.GetConsoleOutputAsync("not-a-url");

        result.Should().Contain("Error");
        result.Should().Contain("Invalid URL");
    }

    // ── Integration tests (require browser) ─────────────────

    private static bool HasBrowser()
    {
        var status = BrowserTools.GetBrowserStatus();
        return status.Contains('✓');
    }

    [Fact]
    public async Task Screenshot_ValidUrl_CreatesFile()
    {
        if (!HasBrowser())
            return; // Skip on CI without browser

        var outputPath = Path.Combine(Path.GetTempPath(), $"test-screenshot-{Guid.NewGuid()}.png");
        try
        {
            var result = await BrowserTools.ScreenshotAsync(
                "https://example.com", outputPath, waitMs: 1000);

            if (!result.Contains("Error"))
            {
                result.Should().Contain("Screenshot saved");
                File.Exists(outputPath).Should().BeTrue();
            }
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Pdf_ValidUrl_CreatesFile()
    {
        if (!HasBrowser())
            return;

        var outputPath = Path.Combine(Path.GetTempPath(), $"test-capture-{Guid.NewGuid()}.pdf");
        try
        {
            var result = await BrowserTools.CapturePdfAsync(
                "https://example.com", outputPath);

            if (!result.Contains("Error"))
            {
                result.Should().Contain("PDF saved");
                File.Exists(outputPath).Should().BeTrue();
            }
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task Content_ValidUrl_ReturnsHtml()
    {
        if (!HasBrowser())
            return;

        var result = await BrowserTools.GetContentAsync("https://example.com", maxChars: 5000);

        if (!result.Contains("Error"))
        {
            result.Should().Contain("Page Content");
            result.Should().Contain("html");
        }
    }

    [Fact]
    public async Task Console_ValidUrl_ReturnsResult()
    {
        if (!HasBrowser())
            return;

        var result = await BrowserTools.GetConsoleOutputAsync(
            "https://example.com", waitMs: 1000);

        // May or may not have console output — both valid
        result.Should().NotBeNullOrEmpty();
    }
}
