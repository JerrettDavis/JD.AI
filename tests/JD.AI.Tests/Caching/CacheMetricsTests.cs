using FluentAssertions;
using JD.AI.Core.Caching;

namespace JD.AI.Tests.Caching;

public sealed class CacheMetricsTests
{
    [Fact]
    public void MeterName_IsCorrect()
    {
        CacheMetrics.MeterName.Should().Be("JD.AI.Cache");
    }

    [Fact]
    public void CacheHits_IsNotNull()
    {
        CacheMetrics.CacheHits.Should().NotBeNull();
    }

    [Fact]
    public void CacheMisses_IsNotNull()
    {
        CacheMetrics.CacheMisses.Should().NotBeNull();
    }

    [Fact]
    public void CacheEvictions_IsNotNull()
    {
        CacheMetrics.CacheEvictions.Should().NotBeNull();
    }

    [Fact]
    public void CacheSize_IsNotNull()
    {
        CacheMetrics.CacheSize.Should().NotBeNull();
    }

    [Fact]
    public void Counters_CanBeIncrementedWithoutError()
    {
        var act = () =>
        {
            CacheMetrics.CacheHits.Add(1);
            CacheMetrics.CacheMisses.Add(1);
            CacheMetrics.CacheEvictions.Add(1);
            CacheMetrics.CacheSize.Add(1);
            CacheMetrics.CacheSize.Add(-1);
        };
        act.Should().NotThrow();
    }
}
