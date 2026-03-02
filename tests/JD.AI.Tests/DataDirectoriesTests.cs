using JD.AI.Core.Config;

namespace JD.AI.Tests;

public sealed class DataDirectoriesTests : IDisposable
{
    private readonly string _tempDir;

    public DataDirectoriesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        DataDirectories.Reset();
    }

    public void Dispose()
    {
        DataDirectories.Reset();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public void SetRoot_OverridesResolution()
    {
        var custom = Path.Combine(_tempDir, "custom");
        DataDirectories.SetRoot(custom);

        Assert.Equal(custom, DataDirectories.Root);
    }

    [Fact]
    public void SessionsDb_ReturnsPathUnderRoot()
    {
        DataDirectories.SetRoot(_tempDir);

        Assert.Equal(Path.Combine(_tempDir, "sessions.db"), DataDirectories.SessionsDb);
    }

    [Fact]
    public void VectorsDb_ReturnsPathUnderRoot()
    {
        DataDirectories.SetRoot(_tempDir);

        Assert.Equal(Path.Combine(_tempDir, "vectors.db"), DataDirectories.VectorsDb);
    }

    [Fact]
    public void OpenClawWorkspace_ReturnsAgentSubdirectory()
    {
        DataDirectories.SetRoot(_tempDir);

        var ws = DataDirectories.OpenClawWorkspace("agent-42");

        Assert.Equal(Path.Combine(_tempDir, "openclaw-workspaces", "agent-42"), ws);
    }

    [Fact]
    public void UpdateCacheDir_ReturnsRoot()
    {
        DataDirectories.SetRoot(_tempDir);

        Assert.Equal(_tempDir, DataDirectories.UpdateCacheDir);
    }

    [Fact]
    public void Reset_ClearsCache_AllowsReresolution()
    {
        DataDirectories.SetRoot(Path.Combine(_tempDir, "first"));
        Assert.Contains("first", DataDirectories.Root);

        DataDirectories.Reset();
        DataDirectories.SetRoot(Path.Combine(_tempDir, "second"));

        Assert.Contains("second", DataDirectories.Root);
    }

    [Fact]
    public void EnvVar_OverridesAll()
    {
        var envDir = Path.Combine(_tempDir, "env-override");
        Environment.SetEnvironmentVariable("JDAI_DATA_DIR", envDir);
        try
        {
            DataDirectories.Reset();

            Assert.Equal(envDir, DataDirectories.Root);
            Assert.True(Directory.Exists(envDir), "Should create the env-specified directory");
        }
        finally
        {
            Environment.SetEnvironmentVariable("JDAI_DATA_DIR", null);
        }
    }
}
