using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Integration tests verifying that PathGuard protections work end-to-end
/// through the actual tool methods, not just the guard in isolation.
/// </summary>
public sealed class PathGuardIntegrationTests : IDisposable
{
    private readonly string _safeDir;

    public PathGuardIntegrationTests()
    {
        _safeDir = Path.Combine(Path.GetTempPath(), $"jdai-guard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_safeDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_safeDir))
            Directory.Delete(_safeDir, recursive: true);
    }

    [Fact]
    public void WriteFile_InSafeDir_Succeeds()
    {
        var path = Path.Combine(_safeDir, "safe.txt");
        var result = FileTools.WriteFile(path, "safe content");

        Assert.Contains("Wrote", result);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void WriteFile_InOpenClawDir_Blocked()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, ".openclaw", "injected.txt");

        var result = FileTools.WriteFile(path, "malicious");

        Assert.StartsWith("Error:", result);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ReadFile_InOpenClawDir_Blocked()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, ".openclaw", "config.json");

        var result = FileTools.ReadFile(path);

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void EditFile_InOpenClawDir_Blocked()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, ".openclaw", "config.json");

        var result = FileTools.EditFile(path, "old", "new");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void ListDirectory_InOpenClawDir_Blocked()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, ".openclaw");

        var result = FileTools.ListDirectory(path);

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void PathTraversal_IntoProtectedDir_Blocked()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Try to reach .openclaw via path traversal from .jdai
        var path = Path.Combine(home, ".jdai", "..", ".openclaw", "pwned.txt");

        var result = FileTools.WriteFile(path, "traversal attack");

        Assert.StartsWith("Error:", result);
    }
}
