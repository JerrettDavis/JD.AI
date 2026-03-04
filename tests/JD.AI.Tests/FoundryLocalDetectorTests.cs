using JD.AI.Core.Providers;
using Xunit;

namespace JD.AI.Tests;

public sealed class FoundryLocalDetectorTests
{
    [Fact]
    public void ProviderName_IsFoundryLocal()
    {
        var detector = new FoundryLocalDetector();
        Assert.Equal("Foundry Local", detector.ProviderName);
    }

    [Fact]
    public async Task DetectAsync_ReturnsUnavailable_WhenEndpointNotReachable()
    {
        // Use an endpoint that is guaranteed to be unreachable in tests
        var detector = new FoundryLocalDetector("http://127.0.0.1:19999");
        var result = await detector.DetectAsync();

        Assert.False(result.IsAvailable);
        Assert.Equal("Not running", result.StatusMessage);
        Assert.Empty(result.Models);
    }

    [Fact]
    public void Constructor_TrimsTrailingSlash()
    {
        var detector = new FoundryLocalDetector("http://127.0.0.1:64646/");
        Assert.Equal("http://127.0.0.1:64646", detector.Endpoint);
    }

    [Fact]
    public void Constructor_PreservesEndpointWithoutSlash()
    {
        var detector = new FoundryLocalDetector("http://127.0.0.1:64646");
        Assert.Equal("http://127.0.0.1:64646", detector.Endpoint);
    }
}
