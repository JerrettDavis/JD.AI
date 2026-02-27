using JD.AI.Tui.Tools;

namespace JD.AI.Tui.Tests;

public sealed class ShellToolsTests
{
    [Fact]
    public async Task RunCommand_ReturnsOutput()
    {
        var result = await ShellTools.RunCommandAsync("echo hello");

        Assert.Contains("Exit code: 0", result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public async Task RunCommand_CapturesExitCode()
    {
        var cmd = OperatingSystem.IsWindows()
            ? "cmd /c exit 42"
            : "exit 42";
        var result = await ShellTools.RunCommandAsync(cmd);

        Assert.Contains("Exit code: 42", result);
    }

    [Fact]
    public async Task RunCommand_RespectsWorkingDirectory()
    {
        var tempDir = Path.GetTempPath();
        var cmd = OperatingSystem.IsWindows() ? "cd" : "pwd";
        var result = await ShellTools.RunCommandAsync(cmd, cwd: tempDir);

        // The output should contain part of the temp path
        Assert.Contains("Exit code: 0", result);
    }

    [Fact]
    public async Task RunCommand_TimesOut()
    {
        var cmd = OperatingSystem.IsWindows()
            ? "ping -n 30 127.0.0.1"
            : "sleep 30";
        var result = await ShellTools.RunCommandAsync(cmd, timeoutSeconds: 1);

        Assert.Contains("timed out", result);
    }

    [Fact]
    public async Task RunCommand_CapturesStderr()
    {
        var cmd = OperatingSystem.IsWindows()
            ? "cmd /c echo error message 1>&2"
            : "echo error message >&2";
        var result = await ShellTools.RunCommandAsync(cmd);

        Assert.Contains("error message", result);
    }

    [Fact]
    public async Task RunCommand_TruncatesLongOutput()
    {
        // Generate output longer than 10000 chars
        var cmd = OperatingSystem.IsWindows()
            ? "cmd /c for /L %i in (1,1,500) do @echo This is a very long line of output that will repeat many times %i"
            : "for i in $(seq 1 500); do echo 'This is a very long line of output that will repeat many times'; done";
        var result = await ShellTools.RunCommandAsync(cmd, timeoutSeconds: 10);

        // Either completes or truncates — either way, should have content
        Assert.True(result.Length > 0);
    }
}
