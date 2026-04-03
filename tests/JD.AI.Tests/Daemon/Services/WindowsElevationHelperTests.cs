using System.Diagnostics;
using JD.AI.Daemon.Services;

namespace JD.AI.Tests.Daemon.Services;

public sealed class WindowsElevationHelperTests
{
    [Fact]
    public void RelaunchAndWait_ReturnsChildExitCode_AndUsesRunAsProcessStartInfo()
    {
        ProcessStartInfo? captured = null;
        using var writer = new StringWriter();

        var exitCode = WindowsElevationHelper.RelaunchAndWait(
            "restart --elevated",
            psi =>
            {
                captured = psi;
                return new WindowsElevationHelper.LaunchResult(true, 7);
            },
            writer,
            @"C:\tools\jdai-daemon.exe");

        Assert.Equal(7, exitCode);
        Assert.NotNull(captured);
        Assert.Equal(@"C:\tools\jdai-daemon.exe", captured!.FileName);
        Assert.Equal("restart --elevated", captured.Arguments);
        Assert.True(captured.UseShellExecute);
        Assert.Equal("runas", captured.Verb);
        Assert.Contains("Waiting for elevated process", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RelaunchAndWait_WhenExecutablePathMissing_ReturnsNull()
    {
        var exitCode = WindowsElevationHelper.RelaunchAndWait(
            "restart --elevated",
            psi => new WindowsElevationHelper.LaunchResult(true, 0),
            output: TextWriter.Null,
            exePath: "");

        Assert.Null(exitCode);
    }

    [Fact]
    public void RelaunchAndWait_WhenLauncherThrows_ReturnsNullAndWritesError()
    {
        using var writer = new StringWriter();

        var exitCode = WindowsElevationHelper.RelaunchAndWait(
            "restart --elevated",
            _ => throw new InvalidOperationException("denied"),
            writer,
            @"C:\tools\jdai-daemon.exe");

        Assert.Null(exitCode);
        Assert.Contains("Unable to relaunch elevated: denied", writer.ToString(), StringComparison.Ordinal);
    }
}
