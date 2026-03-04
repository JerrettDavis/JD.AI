using System.Text.Json;
using JD.AI.Commands;

namespace JD.AI.Tests.Mcp;

/// <summary>
/// Tests for <see cref="McpCliHandler"/> covering argument parsing and output.
/// Uses a temporary JD.AI config file via the JDAI_DATA_DIR environment variable
/// so tests are isolated from real user config.
/// </summary>
[Collection("DataDirectories")]
public sealed class McpCliHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _origDataDir;

    public McpCliHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _origDataDir = Environment.GetEnvironmentVariable("JDAI_DATA_DIR") ?? string.Empty;
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", _tempDir);

        // Reset DataDirectories so it picks up the new env var
        JD.AI.Core.Config.DataDirectories.Reset();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR",
            string.IsNullOrEmpty(_origDataDir) ? null : _origDataDir);
        JD.AI.Core.Config.DataDirectories.Reset();

        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task List_EmptyConfig_ReturnsZero()
    {
        var stdout = await CaptureStdoutAsync(() => McpCliHandler.RunAsync(["list"]));

        Assert.Equal(0, stdout.ExitCode);
        // Other discovery providers (Claude Code, VS Code, etc.) may contribute
        // servers from user-level config, so we only verify exit code and that
        // any JD.AI-managed servers we didn't add are absent.
        Assert.DoesNotContain("jdai-test-server", stdout.Output);
    }

    [Fact]
    public async Task Add_Http_WritesServerAndReturnsZero()
    {
        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(
                ["add", "notion", "--transport", "http", "https://mcp.notion.com/mcp"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("notion", result.Output);

        var listResult = await CaptureStdoutAsync(() => McpCliHandler.RunAsync(["list"]));
        Assert.Contains("notion", listResult.Output);
    }

    [Fact]
    public async Task Add_Stdio_WritesServerAndReturnsZero()
    {
        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(
                ["add", "azure", "--transport", "stdio",
                 "--command", "npx", "--args", "-y", "@azure-devops/mcp", "Quiktrip"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("azure", result.Output);
    }

    [Fact]
    public async Task Add_MissingName_ReturnsOne()
    {
        var result = await CaptureStderrAsync(
            () => McpCliHandler.RunAsync(["add"]));

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Add_MissingTransport_ReturnsOne()
    {
        var result = await CaptureStderrAsync(
            () => McpCliHandler.RunAsync(["add", "myserver"]));

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Add_UnknownTransport_ReturnsOne()
    {
        var result = await CaptureStderrAsync(
            () => McpCliHandler.RunAsync(["add", "srv", "--transport", "ftp", "server.example.com"]));

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Remove_ExistingServer_ReturnsZero()
    {
        await McpCliHandler.RunAsync(
            ["add", "to-remove", "--transport", "http", "https://example.com"]);

        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(["remove", "to-remove"]));

        Assert.Equal(0, result.ExitCode);

        var listResult = await CaptureStdoutAsync(() => McpCliHandler.RunAsync(["list"]));
        Assert.DoesNotContain("to-remove", listResult.Output);
    }

    [Fact]
    public async Task Remove_MissingName_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => McpCliHandler.RunAsync(["remove"]));
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Disable_ExistingServer_UpdatesEnabled()
    {
        await McpCliHandler.RunAsync(
            ["add", "svc", "--transport", "http", "https://example.com"]);

        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(["disable", "svc"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("disabled", result.Output);
    }

    [Fact]
    public async Task Enable_ExistingServer_ReturnsZero()
    {
        await McpCliHandler.RunAsync(
            ["add", "svc", "--transport", "http", "https://example.com"]);
        await McpCliHandler.RunAsync(["disable", "svc"]);

        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(["enable", "svc"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("enabled", result.Output);
    }

    [Fact]
    public async Task List_JsonFlag_ProducesValidJson()
    {
        await McpCliHandler.RunAsync(
            ["add", "notion", "--transport", "http", "https://mcp.notion.com/mcp"]);
        await McpCliHandler.RunAsync(
            ["add", "azure", "--transport", "stdio",
             "--command", "npx", "--args", "-y", "@azure/mcp"]);

        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(["list", "--json"]));

        Assert.Equal(0, result.ExitCode);

        // Must be valid JSON array containing at least the 2 servers we added.
        // Other discovery providers may contribute additional servers from user config.
        using var doc = JsonDocument.Parse(result.Output);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 2,
            $"Expected at least 2 servers, got {doc.RootElement.GetArrayLength()}");

        var names = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("notion", names);
        Assert.Contains("azure", names);
    }

    [Fact]
    public async Task Help_ReturnsZero()
    {
        var result = await CaptureStdoutAsync(
            () => McpCliHandler.RunAsync(["--help"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("jdai mcp", result.Output);
    }

    [Fact]
    public async Task UnknownSubcommand_ReturnsOne()
    {
        var result = await CaptureStderrAsync(
            () => McpCliHandler.RunAsync(["boguscommand"]));

        Assert.Equal(1, result.ExitCode);
    }

    // ── Capture helpers ───────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string Output)> CaptureStdoutAsync(
        Func<Task<int>> action)
    {
        var old = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, sw.ToString());
        }
        finally
        {
            Console.SetOut(old);
        }
    }

    private static async Task<(int ExitCode, string Output)> CaptureStderrAsync(
        Func<Task<int>> action)
    {
        var old = Console.Error;
        using var sw = new StringWriter();
        Console.SetError(sw);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, sw.ToString());
        }
        finally
        {
            Console.SetError(old);
        }
    }
}
