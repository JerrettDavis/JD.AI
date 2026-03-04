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
    public async Task DetectAsync_ReturnsResult_WithoutError()
    {
        // The SDK-based detector should not throw regardless of service state.
        // When the service isn't running, it returns IsAvailable=false.
        var detector = new FoundryLocalDetector();
        var result = await detector.DetectAsync();

        // We can't guarantee the service is running in CI,
        // but the call should always succeed without throwing
        Assert.NotNull(result);
        Assert.Equal("Foundry Local", result.Name);
    }
}
