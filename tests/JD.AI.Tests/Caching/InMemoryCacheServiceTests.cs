using FluentAssertions;
using JD.AI.Core.Caching;

namespace JD.AI.Tests.Caching;

public sealed class InMemoryCacheServiceTests : IDisposable
{
    private readonly InMemoryCacheService _cache = new(maxEntries: 100);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task SetAndGet_ReturnsStoredValue()
    {
        var value = "hello"u8.ToArray();
        await _cache.SetAsync("key1", value);

        var result = await _cache.GetAsync("key1");

        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public async Task Get_NonExistentKey_ReturnsNull()
    {
        var result = await _cache.GetAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Remove_ExistingKey_RemovesEntry()
    {
        await _cache.SetAsync("key1", "data"u8.ToArray());
        await _cache.RemoveAsync("key1");

        var result = await _cache.GetAsync("key1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Exists_ExistingKey_ReturnsTrue()
    {
        await _cache.SetAsync("key1", "data"u8.ToArray());

        (await _cache.ExistsAsync("key1")).Should().BeTrue();
    }

    [Fact]
    public async Task Exists_NonExistentKey_ReturnsFalse()
    {
        (await _cache.ExistsAsync("missing")).Should().BeFalse();
    }

    [Fact]
    public async Task SetWithTtl_ExpiredEntry_ReturnsNull()
    {
        await _cache.SetAsync("key1", "data"u8.ToArray(), ttl: TimeSpan.FromMilliseconds(1));
        await Task.Delay(50); // Wait for expiry

        var result = await _cache.GetAsync("key1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Set_OverCapacity_EvictsOldest()
    {
        using var smallCache = new InMemoryCacheService(maxEntries: 3);

        await smallCache.SetAsync("a", "1"u8.ToArray());
        await smallCache.SetAsync("b", "2"u8.ToArray());
        await smallCache.SetAsync("c", "3"u8.ToArray());
        await smallCache.SetAsync("d", "4"u8.ToArray()); // Should evict "a"

        (await smallCache.GetAsync("a")).Should().BeNull();
        (await smallCache.GetAsync("d")).Should().NotBeNull();
    }

    [Fact]
    public async Task Count_ReflectsEntries()
    {
        await _cache.SetAsync("a", "1"u8.ToArray());
        await _cache.SetAsync("b", "2"u8.ToArray());

        _cache.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetStringAsync_ReturnsString()
    {
        await _cache.SetStringAsync("key", "hello world");

        var result = await _cache.GetStringAsync("key");

        result.Should().Be("hello world");
    }

    [Fact]
    public async Task CaseInsensitiveKeys()
    {
        await _cache.SetAsync("MyKey", "data"u8.ToArray());

        var result = await _cache.GetAsync("mykey");

        result.Should().NotBeNull();
    }
}
