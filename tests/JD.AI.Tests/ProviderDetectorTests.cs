using JD.AI.Tui.Providers;

namespace JD.AI.Tui.Tests;

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
}
