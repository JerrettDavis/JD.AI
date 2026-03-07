using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

/// <summary>
/// Unit tests for <see cref="DetachedUpdater"/>.
/// </summary>
public sealed class DetachedUpdaterTests
{
    // ── InstallResult record ────────────────────────────────────────────

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

    // ── Package ID validation ───────────────────────────────────────────

    [Theory]
    [InlineData("JD.AI")]
    [InlineData("My.Package-1_0")]
    [InlineData("dotnet-tool")]
    public void Launch_SafePackageId_DoesNotThrow(string packageId)
    {
        // On Windows this actually launches — skip actual execution.
        // We just verify that invalid IDs are rejected before writing any script.
        if (OperatingSystem.IsWindows())
        {
            // Launch will succeed (return LaunchedDetached) for a valid ID.
            // We can't prevent the detached window from opening in a real test,
            // so we only test for ArgumentException on invalid IDs.
            return;
        }

        // On non-Windows (CI), no detached process is launched.
        var result = DetachedUpdater.Launch(packageId, null);
        // Either success or a process-not-found error — but NOT an ArgumentException.
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("JD.AI & del /f *.exe")]
    [InlineData("bad;package")]
    [InlineData("evil\r\ninjection")]
    [InlineData("pkg`whoami`")]
    [InlineData("")]
    [InlineData("   ")]
    public void Launch_UnsafePackageId_ReturnsFailure(string packageId)
    {
        var result = DetachedUpdater.Launch(packageId, null);

        Assert.False(result.Success);
        Assert.Contains("not allowed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    // ── Version validation ──────────────────────────────────────────────

    [Theory]
    [InlineData("JD.AI & del /f *.exe")]
    [InlineData("1.0; drop table")]
    [InlineData("../etc/passwd")]
    public void Launch_UnsafeVersion_ReturnsFailure(string badVersion)
    {
        var result = DetachedUpdater.Launch("JD.AI", badVersion);

        Assert.False(result.Success);
        Assert.Contains("not allowed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

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
}

