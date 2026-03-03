using JD.AI.Core.Providers;

namespace JD.AI.Tests;

public sealed class ProviderDetectorTests
{
    [Fact]
    public void FindCli_FindsDotnet()
    {
        // dotnet should always be on PATH in a .NET test environment
        var path = ClaudeCodeDetector.FindCli("dotnet");
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void FindCli_ReturnsNull_ForNonExistentTool()
    {
        var path = ClaudeCodeDetector.FindCli("this-tool-definitely-does-not-exist-xyz");
        Assert.Null(path);
    }

    [Fact]
    public void FindCli_FindsGit()
    {
        // git should be on PATH in CI and dev machines
        var path = ClaudeCodeDetector.FindCli("git");
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void IsServiceAccount_ReturnsFalse_ForNormalUser()
    {
        // A normal user home directory should not be flagged as a service account
        var home = OperatingSystem.IsWindows()
            ? @"C:\Users\someuser"
            : "/home/someuser";

        Assert.False(UserProfileScanner.IsServiceAccount(home));
    }

    [Fact]
    public void IsServiceAccount_ReturnsTrue_ForWindowsServiceProfile()
    {
        if (!OperatingSystem.IsWindows()) return;

        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var svcHome = Path.Combine(winDir, "system32", "config", "systemprofile");

        Assert.True(UserProfileScanner.IsServiceAccount(svcHome));
    }

    [Fact]
    public void FindInUserProfiles_ReturnsNull_ForNonExistentFile()
    {
        var result = UserProfileScanner.FindInUserProfiles(
            "this-directory-definitely-does-not-exist/nonexistent.json");
        Assert.Null(result);
    }
}
