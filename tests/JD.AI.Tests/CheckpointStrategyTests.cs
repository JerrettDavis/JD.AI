using JD.AI.Core.Agents.Checkpointing;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests;

public sealed class CheckpointStrategyTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    // ── DirectoryCheckpointStrategy ────────────────────────

    [Fact]
    public async Task Directory_CreateAsync_ReturnsId()
    {
        var strategy = new DirectoryCheckpointStrategy(_fixture.DirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(_fixture.DirectoryPath, "test.cs"), "hello");

        var id = await strategy.CreateAsync("test-label");

        Assert.NotNull(id);
        Assert.NotEmpty(id!);
    }

    [Fact]
    public async Task Directory_ListAsync_ShowsCreatedCheckpoints()
    {
        var strategy = new DirectoryCheckpointStrategy(_fixture.DirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(_fixture.DirectoryPath, "test.cs"), "v1");
        await strategy.CreateAsync("first");

        // Need unique timestamp — wait a tiny bit to get different second
        await Task.Delay(1100);
        await File.WriteAllTextAsync(Path.Combine(_fixture.DirectoryPath, "test.cs"), "v2");
        await strategy.CreateAsync("second");

        var checkpoints = await strategy.ListAsync();

        Assert.Equal(2, checkpoints.Count);
    }

    [Fact]
    public async Task Directory_RestoreAsync_RestoresContent()
    {
        var strategy = new DirectoryCheckpointStrategy(_fixture.DirectoryPath);
        var filePath = Path.Combine(_fixture.DirectoryPath, "test.cs");
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
        var strategy = new DirectoryCheckpointStrategy(_fixture.DirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(_fixture.DirectoryPath, "test.cs"), "hello");
        await strategy.CreateAsync("label");

        await strategy.ClearAsync();

        var checkpoints = await strategy.ListAsync();
        Assert.Empty(checkpoints);
    }

    [Fact]
    public async Task Directory_RestoreAsync_InvalidId_ReturnsFalse()
    {
        var strategy = new DirectoryCheckpointStrategy(_fixture.DirectoryPath);

        var success = await strategy.RestoreAsync("nonexistent-id");

        Assert.False(success);
    }

    // ── StashCheckpointStrategy (git-dependent) ────────────

    [Fact]
    public async Task Stash_CreateAsync_NoGitRepo_ReturnsNull()
    {
        // _fixture.DirectoryPath is not a git repo, so stash operations should fail gracefully
        var strategy = new StashCheckpointStrategy(_fixture.DirectoryPath);

        var id = await strategy.CreateAsync("test");

        Assert.Null(id);
    }

    [Fact]
    public async Task Stash_ListAsync_NoGitRepo_ReturnsEmpty()
    {
        var strategy = new StashCheckpointStrategy(_fixture.DirectoryPath);

        var checkpoints = await strategy.ListAsync();

        Assert.Empty(checkpoints);
    }
}
