using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class PathGuardTests
{
    [Theory]
    [InlineData("~/.openclaw/config.json")]
    [InlineData("~/.openclaw/skills/my-skill.md")]
    [InlineData("~/.openclaw")]
    public void ThrowsForProtectedOpenClawPaths(string rawPath)
    {
        var expanded = rawPath.Replace("~", Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile));

        var ex = Assert.Throws<PathGuardException>(() =>
            PathGuard.EnsureAllowed(expanded));
        Assert.Contains("protected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("~/.jdai/sessions.db")]
    [InlineData("~/projects/my-app/file.txt")]
    public void AllowsNonProtectedPaths(string rawPath)
    {
        var expanded = rawPath.Replace("~", Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile));

        PathGuard.EnsureAllowed(expanded); // Should not throw
    }

    [Fact]
    public void ThrowsForRelativePathTraversalIntoProtected()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var traversal = Path.Combine(home, ".jdai", "..", ".openclaw", "config.json");

        Assert.Throws<PathGuardException>(() =>
            PathGuard.EnsureAllowed(traversal));
    }

    [Fact]
    public void IsProtected_ReturnsTrueForOpenClaw()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var ocPath = Path.Combine(home, ".openclaw", "config.json");

        Assert.True(PathGuard.IsProtected(ocPath));
    }

    [Fact]
    public void IsProtected_ReturnsFalseForJdaiDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var jdaiPath = Path.Combine(home, ".jdai", "sessions.db");

        Assert.False(PathGuard.IsProtected(jdaiPath));
    }
}
