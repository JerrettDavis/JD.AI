using JD.AI.Commands;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Commands;

[Collection("DataDirectories")]
public sealed class PluginCliHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _origDataDir;

    public PluginCliHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-plugin-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _origDataDir = Environment.GetEnvironmentVariable("JDAI_DATA_DIR") ?? string.Empty;
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", _tempDir);
        DataDirectories.Reset();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", string.IsNullOrEmpty(_origDataDir) ? null : _origDataDir);
        DataDirectories.Reset();

        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task List_EmptyRegistry_ReturnsZero()
    {
        var result = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["list"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No plugins installed.", result.Output);
    }

    [Fact]
    public async Task Help_ReturnsZero()
    {
        var result = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["--help"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("jdai plugin", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Subcommands:", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownSubcommand_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["bogus"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown plugin subcommand", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_MissingSource_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["install"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage: jdai plugin install <path-or-url>", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enable_MissingId_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["enable"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage: jdai plugin enable <id>", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disable_MissingId_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["disable"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage: jdai plugin disable <id>", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_EmptyRegistry_ReturnsZero()
    {
        var result = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["update"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Updated 0 plugin(s).", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uninstall_MissingId_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["uninstall"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage: jdai plugin uninstall <id>", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uninstall_NotInstalled_ReturnsOne()
    {
        var result = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["uninstall", "not-installed"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("is not installed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Info_MissingId_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["info"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage: jdai plugin info <id>", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Info_NotInstalled_ReturnsOne()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["info", "not-installed"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enable_NotInstalled_ReturnsOneWithError()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["enable", "missing"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Plugin command failed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disable_NotInstalled_ReturnsOneWithError()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["disable", "missing"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Plugin command failed", result.Output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> CaptureStdoutAsync(Func<Task<int>> action)
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

    private static async Task<(int ExitCode, string Output)> CaptureStderrAsync(Func<Task<int>> action)
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
