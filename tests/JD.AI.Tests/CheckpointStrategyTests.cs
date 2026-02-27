using JD.AI.Tui.Agent.Checkpointing;

namespace JD.AI.Tui.Tests;

public sealed class CheckpointStrategyTests : IDisposable
{
    private readonly string _tempDir;

    public CheckpointStrategyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-cp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    // ── DirectoryCheckpointStrategy ────────────────────────

    [Fact]
    public async Task Directory_CreateAsync_ReturnsId()
    {
        var strategy = new DirectoryCheckpointStrategy(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.cs"), "hello");

        var id = await strategy.CreateAsync("test-label");

        Assert.NotNull(id);
        Assert.NotEmpty(id!);
    }

    [Fact]
    public async Task Directory_ListAsync_ShowsCreatedCheckpoints()
    {
        var strategy = new DirectoryCheckpointStrategy(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.cs"), "v1");
        await strategy.CreateAsync("first");

        // Need unique timestamp — wait a tiny bit to get different second
        await Task.Delay(1100);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.cs"), "v2");
        await strategy.CreateAsync("second");

        var checkpoints = await strategy.ListAsync();

        Assert.Equal(2, checkpoints.Count);
    }

    [Fact]
    public async Task Directory_RestoreAsync_RestoresContent()
    {
        var strategy = new DirectoryCheckpointStrategy(_tempDir);
        var filePath = Path.Combine(_tempDir, "test.cs");
        await File.WriteAllTextAsync(filePath, "original");
        var id = await strategy.CreateAsync("before-change");
        Assert.NotNull(id);

        await File.WriteAllTextAsync(filePath, "modified");
        var success = await strategy.RestoreAsync(id!);

        Assert.True(success);
        Assert.Equal("original", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task Directory_ClearAsync_RemovesAll()
    {
        var strategy = new DirectoryCheckpointStrategy(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.cs"), "hello");
        await strategy.CreateAsync("label");

        await strategy.ClearAsync();

        var checkpoints = await strategy.ListAsync();
        Assert.Empty(checkpoints);
    }

    [Fact]
    public async Task Directory_RestoreAsync_InvalidId_ReturnsFalse()
    {
        var strategy = new DirectoryCheckpointStrategy(_tempDir);

        var success = await strategy.RestoreAsync("nonexistent-id");

        Assert.False(success);
    }

    // ── StashCheckpointStrategy (git-dependent) ────────────

    [Fact]
    public async Task Stash_CreateAsync_NoGitRepo_ReturnsNull()
    {
        // _tempDir is not a git repo, so stash operations should fail gracefully
        var strategy = new StashCheckpointStrategy(_tempDir);

        var id = await strategy.CreateAsync("test");

        Assert.Null(id);
    }

    [Fact]
    public async Task Stash_ListAsync_NoGitRepo_ReturnsEmpty()
    {
        var strategy = new StashCheckpointStrategy(_tempDir);

        var checkpoints = await strategy.ListAsync();

        Assert.Empty(checkpoints);
    }
}
