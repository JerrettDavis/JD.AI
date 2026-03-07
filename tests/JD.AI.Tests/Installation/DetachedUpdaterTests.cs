using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

/// <summary>
/// Unit tests for <see cref="DetachedUpdater"/>.
/// </summary>
public sealed class DetachedUpdaterTests
{
    [Fact]
    public void Launch_ReturnsLaunchedDetached_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return; // Windows-only path

        var result = DetachedUpdater.Launch("JD.AI", null);

        Assert.True(result.LaunchedDetached);
        Assert.True(result.Success);
        Assert.True(result.RequiresRestart);
        Assert.Contains("launched", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Launch_WithVersion_ReturnsLaunchedDetached_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = DetachedUpdater.Launch("JD.AI", "1.2.3");

        Assert.True(result.LaunchedDetached);
        Assert.True(result.Success);
    }

    [Fact]
    public void InstallResult_LaunchedDetached_DefaultIsFalse()
    {
        var result = new InstallResult(true, "OK");

        Assert.False(result.LaunchedDetached);
    }

    [Fact]
    public void InstallResult_AllFields_RoundTrip()
    {
        var result = new InstallResult(
            Success: false,
            Output: "test output",
            RequiresRestart: true,
            LaunchedDetached: true);

        Assert.False(result.Success);
        Assert.Equal("test output", result.Output);
        Assert.True(result.RequiresRestart);
        Assert.True(result.LaunchedDetached);
    }

    [Fact]
    public void InstallResult_IsRecord_EqualityByValue()
    {
        var a = new InstallResult(true, "same", false, false);
        var b = new InstallResult(true, "same", false, false);

        Assert.Equal(a, b);
    }

    [Fact]
    public void InstallResult_Deconstruct_WorksCorrectly()
    {
        var result = new InstallResult(true, "output", RequiresRestart: false, LaunchedDetached: true);

        Assert.True(result.LaunchedDetached);
        Assert.Equal("output", result.Output);
    }
}
