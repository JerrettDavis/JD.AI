using FluentAssertions;
using JD.AI.Core.Caching;

namespace JD.AI.Tests.Caching;

public sealed class ToolResultCacheTests : IDisposable
{
    private readonly InMemoryCacheService _backingCache = new();
    private readonly ToolResultCache _toolCache;

    public ToolResultCacheTests()
    {
        _toolCache = new ToolResultCache(
            _backingCache,
            cacheableTools: ["read_file", "web_fetch"],
            defaultTtl: TimeSpan.FromMinutes(5),
            toolTtls: new Dictionary<string, TimeSpan>
            {
                ["web_fetch"] = TimeSpan.FromMinutes(1),
            });
    }

    public void Dispose() => _backingCache.Dispose();

    [Fact]
    public async Task GetCachedResult_NoCachedEntry_ReturnsNull()
    {
        var result = await _toolCache.GetCachedResultAsync("read_file", "{\"path\":\"/tmp/test\"}");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAndGet_CacheableToolResult_RoundTrips()
    {
        const string args = "{\"path\":\"/tmp/test\"}";
        const string resultContent = "file contents here";

        await _toolCache.SetResultAsync("read_file", args, resultContent);
        var cached = await _toolCache.GetCachedResultAsync("read_file", args);

        cached.Should().Be(resultContent);
    }

    [Fact]
    public async Task SetResult_NonCacheableTool_DoesNotCache()
    {
        await _toolCache.SetResultAsync("shell_exec", "ls", "output");
        var cached = await _toolCache.GetCachedResultAsync("shell_exec", "ls");

        cached.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedResult_DifferentArgs_ReturnsDifferentResults()
    {
        await _toolCache.SetResultAsync("read_file", "{\"path\":\"/a\"}", "content A");
        await _toolCache.SetResultAsync("read_file", "{\"path\":\"/b\"}", "content B");

        var a = await _toolCache.GetCachedResultAsync("read_file", "{\"path\":\"/a\"}");
        var b = await _toolCache.GetCachedResultAsync("read_file", "{\"path\":\"/b\"}");

        a.Should().Be("content A");
        b.Should().Be("content B");
    }

    [Fact]
    public void IsCacheable_RegisteredTool_ReturnsTrue()
    {
        _toolCache.IsCacheable("read_file").Should().BeTrue();
    }

    [Fact]
    public void IsCacheable_UnregisteredTool_ReturnsFalse()
    {
        _toolCache.IsCacheable("shell_exec").Should().BeFalse();
    }

    [Fact]
    public async Task HitsMisses_TrackCorrectly()
    {
        await _toolCache.SetResultAsync("read_file", "args", "result");

        await _toolCache.GetCachedResultAsync("read_file", "args"); // Hit
        await _toolCache.GetCachedResultAsync("read_file", "other"); // Miss

        _toolCache.Hits.Should().Be(1);
        _toolCache.Misses.Should().Be(1);
    }

    [Fact]
    public void IsCacheable_CaseInsensitive()
    {
        _toolCache.IsCacheable("READ_FILE").Should().BeTrue();
    }
}
