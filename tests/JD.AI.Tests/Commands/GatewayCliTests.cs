using JD.AI.Commands;
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
        // GatewayHealthChecker uses 127.0.0.1 as default host to avoid resolution issues,
        // while it still uses the default port from runtime defaults.
        var expected = $"http://127.0.0.1:{GatewayRuntimeDefaults.DefaultPort}";
        Assert.Equal(expected, GatewayHealthChecker.DefaultBaseUrl);
    }

    [Fact]
    public async Task GatewayCliHandler_RunRestartAsync_InvokesDaemonRestart()
    {
        var receivedArguments = new List<string>();

        var exitCode = await GatewayCliHandler.RunRestartAsync(
            daemonArguments =>
            {
                receivedArguments.Add(daemonArguments);
                return Task.FromResult(new GatewayCliHandler.DaemonCommandResult(true, "restarted"));
            });

        Assert.Equal(0, exitCode);
        Assert.Equal(["restart"], receivedArguments);
    }

    [Fact]
    public async Task GatewayCliHandler_RunRestartAsync_WhenDaemonRestartFails_ReturnsFailure()
    {
        var exitCode = await GatewayCliHandler.RunRestartAsync(
            _ => Task.FromResult(new GatewayCliHandler.DaemonCommandResult(false, "boom")));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task GatewayCliHandler_RunRestartAsync_DoesNotProbeHealthAfterDelegatingToDaemon()
    {
        var exitCode = await GatewayCliHandler.RunRestartAsync(
            _ => Task.FromResult(new GatewayCliHandler.DaemonCommandResult(true, "restarted")));

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task GatewayCliHandler_RunRestartAsync_WhenServiceIsNotInstalled_ShowsForegroundGuidance()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await GatewayCliHandler.RunRestartAsync(
                _ => Task.FromResult(new GatewayCliHandler.DaemonCommandResult(false, "Service is not installed.")));

            Assert.Equal(1, exitCode);
            Assert.Contains("Use 'jdai gateway start' for the foreground gateway", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Theory]
    [InlineData(null, "start")]
    [InlineData("start", "start")]
    [InlineData("restart", "restart")]
    [InlineData(" Restart ", "restart")]
    public void GatewayCliHandler_NormalizeAction_MapsExpectedValues(string? action, string expected)
    {
        var args = action is null ? Array.Empty<string>() : [action];
        Assert.Equal(expected, GatewayCliHandler.NormalizeAction(args));
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
