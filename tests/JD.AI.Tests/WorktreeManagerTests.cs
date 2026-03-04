using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class WorktreeManagerTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var manager = new WorktreeManager("/tmp/repo");
        Assert.Null(manager.WorktreePath);
        Assert.Null(manager.BranchName);
    }

    [Fact]
    public async Task DisposeAsync_WhenNoWorktree_DoesNotThrow()
    {
        var manager = new WorktreeManager("/tmp/repo");
        await manager.DisposeAsync();
        // Should not throw
    }

    [Fact]
    public async Task CreateAsync_WhenNotGitRepo_ThrowsInvalidOperation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"wt-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var manager = new WorktreeManager(tempDir);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.CreateAsync());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
