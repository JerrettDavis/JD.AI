using FluentAssertions;
using JD.AI.Core.Providers.Credentials;

namespace JD.AI.Tests.Providers.Credentials;

public sealed class EncryptedFileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EncryptedFileStore _store;

    public EncryptedFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-efstore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new EncryptedFileStore(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _store.GetAsync("nonexistent-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        await _store.SetAsync("test-key", "test-value");

        var result = await _store.GetAsync("test-key");

        result.Should().Be("test-value");
    }

    [Fact]
    public async Task RemoveAsync_RemovesKey()
    {
        await _store.SetAsync("remove-me", "value");

        await _store.RemoveAsync("remove-me");

        var result = await _store.GetAsync("remove-me");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListKeysAsync_ReturnsMatchingKeys()
    {
        await _store.SetAsync("prefix:alpha", "a");
        await _store.SetAsync("prefix:beta", "b");
        await _store.SetAsync("other:gamma", "c");

        var keys = await _store.ListKeysAsync("prefix:");

        keys.Should().HaveCount(2);
        keys.Should().Contain(k => k.Equals("prefix:alpha", StringComparison.OrdinalIgnoreCase));
        keys.Should().Contain(k => k.Equals("prefix:beta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetAsync_Overwrite_ReturnsNewValue()
    {
        await _store.SetAsync("overwrite-key", "old-value");

        await _store.SetAsync("overwrite-key", "new-value");

        var result = await _store.GetAsync("overwrite-key");
        result.Should().Be("new-value");
    }
}
