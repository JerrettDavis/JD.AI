using FluentAssertions;
using JD.AI.Core.Config;

namespace JD.AI.Tests.Config;

/// <summary>
/// Tests DataDirectories path resolution using SetRoot/Reset.
/// Uses a dedicated test collection to avoid parallel conflicts with other
/// tests that use DataDirectories.
/// </summary>
[Collection("DataDirectories")]
public sealed class DataDirectoriesExtendedTests : IDisposable
{
    private readonly string _tempDir;

    public DataDirectoriesExtendedTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-dd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        DataDirectories.SetRoot(_tempDir);
    }

    public void Dispose()
    {
        DataDirectories.Reset();
        try { Directory.Delete(_tempDir, true); } catch { /* cleanup best-effort */ }
    }

    [Fact]
    public void SetRoot_SetsRootPath()
    {
        DataDirectories.Root.Should().Be(_tempDir);
    }

    [Fact]
    public void SessionsDb_CombinesWithRoot()
    {
        DataDirectories.SessionsDb.Should().Be(Path.Combine(_tempDir, "sessions.db"));
    }

    [Fact]
    public void VectorsDb_CombinesWithRoot()
    {
        DataDirectories.VectorsDb.Should().Be(Path.Combine(_tempDir, "vectors.db"));
    }

    [Fact]
    public void UsageDb_CombinesWithRoot()
    {
        DataDirectories.UsageDb.Should().Be(Path.Combine(_tempDir, "usage.db"));
    }

    [Fact]
    public void UpdateCacheDir_EqualsRoot()
    {
        DataDirectories.UpdateCacheDir.Should().Be(_tempDir);
    }

    [Fact]
    public void OpenClawWorkspace_IncludesAgentId()
    {
        var path = DataDirectories.OpenClawWorkspace("agent-42");
        path.Should().Be(Path.Combine(_tempDir, "openclaw-workspaces", "agent-42"));
    }

    [Fact]
    public void OrgConfigPath_NullWhenNoEnvOrFile()
    {
        // With a fresh temp dir, no org-config-path file exists
        DataDirectories.OrgConfigPath.Should().BeNull();
    }

    [Fact]
    public void OrgConfigPath_ReadsFromFile()
    {
        var orgDir = Path.Combine(_tempDir, "org-config");
        Directory.CreateDirectory(orgDir);
        File.WriteAllText(Path.Combine(_tempDir, "org-config-path"), orgDir);

        DataDirectories.OrgConfigPath.Should().Be(orgDir);
    }

    [Fact]
    public void OrgConfigPath_IgnoresNonExistentStoredPath()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "org-config-path"),
            "/nonexistent/path/that/does/not/exist");

        DataDirectories.OrgConfigPath.Should().BeNull();
    }

    [Fact]
    public void Reset_AllowsNewSetRoot()
    {
        DataDirectories.Reset();
        var newDir = Path.Combine(Path.GetTempPath(), $"jdai-dd2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(newDir);
        try
        {
            DataDirectories.SetRoot(newDir);
            DataDirectories.Root.Should().Be(newDir);
        }
        finally
        {
            DataDirectories.Reset();
            try { Directory.Delete(newDir, true); } catch { /* cleanup */ }
        }
    }
}
