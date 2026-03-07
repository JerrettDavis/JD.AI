using JD.AI.Core.Config;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests;

[Collection("DataDirectories")]
public sealed class DataDirectoriesTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public DataDirectoriesTests()
    {
        DataDirectories.Reset();
    }

    public void Dispose()
    {
        DataDirectories.Reset();
        _fixture.Dispose();
    }

    [Fact]
    public void SetRoot_OverridesResolution()
    {
        var custom = Path.Combine(_fixture.DirectoryPath, "custom");
        DataDirectories.SetRoot(custom);

        Assert.Equal(custom, DataDirectories.Root);
    }

    [Fact]
    public void SessionsDb_ReturnsPathUnderRoot()
    {
        DataDirectories.SetRoot(_fixture.DirectoryPath);

        Assert.Equal(Path.Combine(_fixture.DirectoryPath, "sessions.db"), DataDirectories.SessionsDb);
    }

    [Fact]
    public void VectorsDb_ReturnsPathUnderRoot()
    {
        DataDirectories.SetRoot(_fixture.DirectoryPath);

        Assert.Equal(Path.Combine(_fixture.DirectoryPath, "vectors.db"), DataDirectories.VectorsDb);
    }

    [Fact]
    public void OpenClawWorkspace_ReturnsAgentSubdirectory()
    {
        DataDirectories.SetRoot(_fixture.DirectoryPath);

        var ws = DataDirectories.OpenClawWorkspace("agent-42");

        Assert.Equal(Path.Combine(_fixture.DirectoryPath, "openclaw-workspaces", "agent-42"), ws);
    }

    [Fact]
    public void UpdateCacheDir_ReturnsRoot()
    {
        DataDirectories.SetRoot(_fixture.DirectoryPath);

        Assert.Equal(_fixture.DirectoryPath, DataDirectories.UpdateCacheDir);
    }

    [Fact]
    public void Reset_ClearsCache_AllowsReresolution()
    {
        DataDirectories.SetRoot(Path.Combine(_fixture.DirectoryPath, "first"));
        Assert.Contains("first", DataDirectories.Root);

        DataDirectories.Reset();
        DataDirectories.SetRoot(Path.Combine(_fixture.DirectoryPath, "second"));

        Assert.Contains("second", DataDirectories.Root);
    }

    [Fact]
    public void EnvVar_OverridesAll()
    {
        var envDir = Path.Combine(_fixture.DirectoryPath, "env-override");
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
