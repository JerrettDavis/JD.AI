using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Tests.Commands;

/// <summary>
/// Tests for the gateway/dashboard CLI utilities.
/// </summary>
public sealed class GatewayCliTests
{
    [Fact]
    public async Task GatewayHealthChecker_ReturnsFalse_WhenNothingIsRunning()
    {
        // Use a port that is almost certainly not in use
        var result = await GatewayHealthChecker.IsRunningAsync("http://localhost:19999", timeoutMs: 500);
        Assert.False(result);
    }

    [Fact]
    public async Task GatewayHealthChecker_WaitForHealthy_ReturnsFalse_WhenNothingIsRunning()
    {
        var result = await GatewayHealthChecker.WaitForHealthyAsync("http://localhost:19999", maxWaitMs: 1000);
        Assert.False(result);
    }

    [Fact]
    public void GatewayHealthChecker_DefaultBaseUrl_UsesRuntimeDefaults()
    {
        var expected = $"http://{GatewayRuntimeDefaults.DefaultHost}:{GatewayRuntimeDefaults.DefaultPort}";
        Assert.Equal(expected, GatewayHealthChecker.DefaultBaseUrl);
    }

    [Fact]
    public void BrowserLauncher_Open_DoesNotThrow()
    {
        // Verify that calling Open with a dummy URL does not throw.
        // The actual browser launch is a fire-and-forget side effect;
        // we just verify no exception is raised on the current platform.
        var ex = Record.Exception(() => BrowserLauncher.Open("http://localhost:0/test"));
        Assert.Null(ex);
    }
}
