using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyWatcherTests : IDisposable
{
    private readonly string _tempDir;

    public PolicyWatcherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-pw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void Constructor_ValidDirectory_CreatesWatchers()
    {
        using var watcher = new PolicyWatcher([_tempDir], () => { });

        // Two watchers per directory (*.yaml + *.yml)
        watcher.WatcherCount.Should().Be(2);
    }

    [Fact]
    public void Constructor_NonExistentDirectory_SkipsIt()
    {
        var nonExistent = Path.Combine(_tempDir, "does-not-exist");

        using var watcher = new PolicyWatcher([nonExistent], () => { });

        watcher.WatcherCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_MultipleDirectories_CreatesWatchersForEach()
    {
        var dir2 = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(dir2);

        using var watcher = new PolicyWatcher([_tempDir, dir2], () => { });

        watcher.WatcherCount.Should().Be(4); // 2 per directory
    }

    [Fact]
    public async Task FileChange_TriggersReload()
    {
        var reloadTriggered = new TaskCompletionSource<bool>();

        using var watcher = new PolicyWatcher(
            [_tempDir],
            () => reloadTriggered.TrySetResult(true),
            debounce: TimeSpan.FromMilliseconds(50));

        // Create a yaml file to trigger the watcher
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.yaml"), "content: test");

        var result = await Task.WhenAny(
            reloadTriggered.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        result.Should().Be(reloadTriggered.Task, "reload should have been triggered");
    }

    [Fact]
    public async Task YmlFileChange_AlsoTriggersReload()
    {
        var reloadTriggered = new TaskCompletionSource<bool>();

        using var watcher = new PolicyWatcher(
            [_tempDir],
            () => reloadTriggered.TrySetResult(true),
            debounce: TimeSpan.FromMilliseconds(50));

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.yml"), "content: test");

        var result = await Task.WhenAny(
            reloadTriggered.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        result.Should().Be(reloadTriggered.Task, "reload should have been triggered for .yml");
    }

    [Fact]
    public void Dispose_ClearsWatchers()
    {
        var watcher = new PolicyWatcher([_tempDir], () => { });
        watcher.WatcherCount.Should().Be(2);

        watcher.Dispose();

        watcher.WatcherCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var watcher = new PolicyWatcher([_tempDir], () => { });

        var act = () =>
        {
            watcher.Dispose();
            watcher.Dispose();
        };

        act.Should().NotThrow();
    }
}
