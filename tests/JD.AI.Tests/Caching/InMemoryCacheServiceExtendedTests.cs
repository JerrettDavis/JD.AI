using FluentAssertions;
using JD.AI.Core.Caching;

namespace JD.AI.Tests.Caching;

/// <summary>
/// Extended tests for <see cref="InMemoryCacheService"/> covering expiry
/// via ExistsAsync, background sweep, and Dispose idempotency.
/// Base CRUD/eviction/TTL tests are in <see cref="InMemoryCacheServiceTests"/>.
/// </summary>
public sealed class InMemoryCacheServiceExtendedTests
{
    // ── ExistsAsync expiry removal ────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_ExpiredEntry_ReturnsFalseAndRemoves()
    {
        using var cache = new InMemoryCacheService();
        await cache.SetAsync("expiring", "data"u8.ToArray(), ttl: TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        (await cache.ExistsAsync("expiring")).Should().BeFalse();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task ExistsAsync_NonExpiredEntry_ReturnsTrue()
    {
        using var cache = new InMemoryCacheService();
        await cache.SetAsync("alive", "data"u8.ToArray(), ttl: TimeSpan.FromMinutes(5));

        (await cache.ExistsAsync("alive")).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NoTtl_NeverExpires()
    {
        using var cache = new InMemoryCacheService();
        await cache.SetAsync("forever", "data"u8.ToArray());

        (await cache.ExistsAsync("forever")).Should().BeTrue();
    }

    // ── Background sweep ──────────────────────────────────────────────────

    [Fact]
    public async Task Sweep_RemovesExpiredEntries()
    {
        // Short sweep interval so the timer fires quickly
        using var cache = new InMemoryCacheService(sweepInterval: TimeSpan.FromMilliseconds(50));

        await cache.SetAsync("short-lived", "data"u8.ToArray(), ttl: TimeSpan.FromMilliseconds(1));
        await cache.SetAsync("long-lived", "data"u8.ToArray(), ttl: TimeSpan.FromMinutes(5));

        // Wait for sweep to fire
        await Task.Delay(200);

        cache.Count.Should().Be(1);
        (await cache.GetAsync("short-lived")).Should().BeNull();
        (await cache.GetAsync("long-lived")).Should().NotBeNull();
    }

    // ── Dispose idempotency ───────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var cache = new InMemoryCacheService();
        cache.Dispose();

        var act = () => cache.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_ClearsAllEntries()
    {
        var cache = new InMemoryCacheService();
        await cache.SetAsync("key1", "data"u8.ToArray());
        await cache.SetAsync("key2", "data"u8.ToArray());

        cache.Dispose();

        cache.Count.Should().Be(0);
    }

    // ── Overwrite existing key ────────────────────────────────────────────

    [Fact]
    public async Task Set_SameKey_OverwritesValue()
    {
        using var cache = new InMemoryCacheService();
        await cache.SetAsync("key", "old"u8.ToArray());
        await cache.SetAsync("key", "new"u8.ToArray());

        var result = await cache.GetAsync("key");
        result.Should().BeEquivalentTo("new"u8.ToArray());
        cache.Count.Should().Be(1);
    }

    // ── Remove non-existent key ───────────────────────────────────────────

    [Fact]
    public async Task Remove_NonExistentKey_DoesNotThrow()
    {
        using var cache = new InMemoryCacheService();

        var act = async () => await cache.RemoveAsync("nope");

        await act.Should().NotThrowAsync();
    }
}
