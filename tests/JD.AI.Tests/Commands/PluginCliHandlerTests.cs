using JD.AI.Commands;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Commands;

[Collection("DataDirectories")]
public sealed class PluginCliHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalDataDir;

    public PluginCliHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-plugin-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _originalDataDir = Environment.GetEnvironmentVariable("JDAI_DATA_DIR") ?? string.Empty;
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", _tempDir);
        DataDirectories.Reset();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR",
            string.IsNullOrEmpty(_originalDataDir) ? null : _originalDataDir);
        DataDirectories.Reset();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task HelpUnknownAndListPaths_AreHandled()
    {
        var help = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["help"]));
        var unknown = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["bogus"]));
        var list = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["list"]));

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("jdai plugin", help.Output, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("Unknown plugin subcommand", unknown.Output, StringComparison.Ordinal);

        Assert.Equal(0, list.ExitCode);
        Assert.Contains("No plugins installed", list.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingArguments_ReturnUsageErrors()
    {
        var install = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["install"]));
        var enable = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["enable"]));
        var disable = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["disable"]));
        var uninstall = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["uninstall"]));
        var info = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["info"]));

        Assert.Equal(1, install.ExitCode);
        Assert.Contains("Usage: jdai plugin install", install.Output, StringComparison.Ordinal);

        Assert.Equal(1, enable.ExitCode);
        Assert.Contains("Usage: jdai plugin enable", enable.Output, StringComparison.Ordinal);

        Assert.Equal(1, disable.ExitCode);
        Assert.Contains("Usage: jdai plugin disable", disable.Output, StringComparison.Ordinal);

        Assert.Equal(1, uninstall.ExitCode);
        Assert.Contains("Usage: jdai plugin uninstall", uninstall.Output, StringComparison.Ordinal);

        Assert.Equal(1, info.ExitCode);
        Assert.Contains("Usage: jdai plugin info", info.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InfoAndUninstall_ForUnknownPlugin_ReturnExpectedErrors()
    {
        var info = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["info", "missing-plugin"]));
        var uninstall = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["uninstall", "missing-plugin"]));
        var updateAll = await CaptureStdoutAsync(() => PluginCliHandler.RunAsync(["update"]));

        Assert.Equal(1, info.ExitCode);
        Assert.Contains("not found", info.Output, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, uninstall.ExitCode);
        Assert.Contains("is not installed", uninstall.Output, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(0, updateAll.ExitCode);
        Assert.Contains("Updated 0 plugin(s)", updateAll.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstallInvalidSource_UsesTopLevelErrorHandler()
    {
        var result = await CaptureStderrAsync(() => PluginCliHandler.RunAsync(["install", "not-a-plugin-source"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Plugin command failed", result.Output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> CaptureStdoutAsync(Func<Task<int>> action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static async Task<(int ExitCode, string Output)> CaptureStderrAsync(Func<Task<int>> action)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            var code = await action().ConfigureAwait(false);
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
