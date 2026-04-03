using JD.AI.Commands;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;

namespace JD.AI.Tests.Commands;

/// <summary>
/// Tests for the gateway/dashboard CLI utilities.
/// </summary>
[Collection("Console")]
public sealed class GatewayCliTests
{
    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenGatewayAlreadyRunning_OpensBrowserAndSkipsDaemonStart()
    {
        using var restore = new DashboardCliOverrideScope();
        var started = false;
        string? openedUrl = null;

        DashboardCliHandler.GetRunningGatewayBaseUrlAsync = () => Task.FromResult<string?>("http://localhost:15790");
        DashboardCliHandler.StartDaemonProcess = () =>
        {
            started = true;
            return new FakeDaemonProcessHandle();
        };
        DashboardCliHandler.OpenBrowser = url => openedUrl = url;

        var exitCode = await DashboardCliHandler.RunAsync([]);

        Assert.Equal(0, exitCode);
        Assert.False(started);
        Assert.Equal("http://localhost:15790/", openedUrl);
    }

    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenArgsProvided_ReturnsUsageError()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await DashboardCliHandler.RunAsync(["unexpected"]);

            Assert.Equal(1, exitCode);
            Assert.Contains("Usage: jdai dashboard", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenDaemonStartFails_ReturnsFailure()
    {
        using var restore = new DashboardCliOverrideScope();

        DashboardCliHandler.GetRunningGatewayBaseUrlAsync = () => Task.FromResult<string?>(null);
        DashboardCliHandler.StartDaemonProcess = () => throw new InvalidOperationException("boom");

        var exitCode = await DashboardCliHandler.RunAsync([]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenGatewayDoesNotBecomeHealthy_KillsProcess()
    {
        using var restore = new DashboardCliOverrideScope();
        var process = new FakeDaemonProcessHandle();

        DashboardCliHandler.GetRunningGatewayBaseUrlAsync = () => Task.FromResult<string?>(null);
        DashboardCliHandler.StartDaemonProcess = () => process;
        DashboardCliHandler.WaitForGatewayHealthyBaseUrlAsync = _ => Task.FromResult<string?>(null);

        var exitCode = await DashboardCliHandler.RunAsync([]);

        Assert.Equal(1, exitCode);
        Assert.True(process.KillCalled);
        Assert.True(process.DisposeCalled);
    }

    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenGatewayDoesNotBecomeHealthyAndProcessAlreadyExited_StillDisposes()
    {
        using var restore = new DashboardCliOverrideScope();
        var process = new FakeDaemonProcessHandle { HasExited = true };

        DashboardCliHandler.GetRunningGatewayBaseUrlAsync = () => Task.FromResult<string?>(null);
        DashboardCliHandler.StartDaemonProcess = () => process;
        DashboardCliHandler.WaitForGatewayHealthyBaseUrlAsync = _ => Task.FromResult<string?>(null);

        var exitCode = await DashboardCliHandler.RunAsync([]);

        Assert.Equal(1, exitCode);
        Assert.False(process.KillCalled);
        Assert.True(process.DisposeCalled);
    }

    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenGatewayBecomesHealthy_OpensReachableUrlAndDisposesProcess()
    {
        using var restore = new DashboardCliOverrideScope();
        var process = new FakeDaemonProcessHandle();
        string? openedUrl = null;

        DashboardCliHandler.GetRunningGatewayBaseUrlAsync = () => Task.FromResult<string?>(null);
        DashboardCliHandler.StartDaemonProcess = () => process;
        DashboardCliHandler.WaitForGatewayHealthyBaseUrlAsync = _ => Task.FromResult<string?>("http://localhost:15790");
        DashboardCliHandler.OpenBrowser = url => openedUrl = url;

        var exitCode = await DashboardCliHandler.RunAsync([]);

        Assert.Equal(0, exitCode);
        Assert.False(process.KillCalled);
        Assert.True(process.DisposeCalled);
        Assert.Equal("http://localhost:15790/", openedUrl);
    }

    [Fact]
    public async Task DashboardCliHandler_RunAsync_WhenBrowserOpenFails_ReturnsFailureAndStillDisposesProcess()
    {
        using var restore = new DashboardCliOverrideScope();
        var process = new FakeDaemonProcessHandle();
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            DashboardCliHandler.GetRunningGatewayBaseUrlAsync = () => Task.FromResult<string?>(null);
            DashboardCliHandler.StartDaemonProcess = () => process;
            DashboardCliHandler.WaitForGatewayHealthyBaseUrlAsync = _ => Task.FromResult<string?>("http://localhost:15790");
            DashboardCliHandler.OpenBrowser = _ => throw new InvalidOperationException("browser failed");

            var exitCode = await DashboardCliHandler.RunAsync([]);

            Assert.Equal(1, exitCode);
            Assert.True(process.DisposeCalled);
            Assert.Contains("Failed to open dashboard automatically", writer.ToString(), StringComparison.Ordinal);
            Assert.Contains("Open this URL manually: http://localhost:15790/", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

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
    public async Task GatewayCliHandler_RunAsync_DefaultsToStart()
    {
        var startCalls = 0;
        var restartCalls = 0;

        var exitCode = await GatewayCliHandler.RunAsync(
            Array.Empty<string>(),
            () =>
            {
                startCalls++;
                return Task.FromResult(12);
            },
            () =>
            {
                restartCalls++;
                return Task.FromResult(34);
            });

        Assert.Equal(12, exitCode);
        Assert.Equal(1, startCalls);
        Assert.Equal(0, restartCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunAsync_DispatchesRestartAction()
    {
        var startCalls = 0;
        var restartCalls = 0;

        var exitCode = await GatewayCliHandler.RunAsync(
            [" restart "],
            () =>
            {
                startCalls++;
                return Task.FromResult(12);
            },
            () =>
            {
                restartCalls++;
                return Task.FromResult(34);
            });

        Assert.Equal(34, exitCode);
        Assert.Equal(0, startCalls);
        Assert.Equal(1, restartCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunAsync_WhenActionIsUnknown_WritesUsageAndSkipsHandlers()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        var startCalls = 0;
        var restartCalls = 0;

        try
        {
            var exitCode = await GatewayCliHandler.RunAsync(
                ["status"],
                () =>
                {
                    startCalls++;
                    return Task.FromResult(12);
                },
                () =>
                {
                    restartCalls++;
                    return Task.FromResult(34);
                });

            Assert.Equal(1, exitCode);
            Assert.Equal(0, startCalls);
            Assert.Equal(0, restartCalls);
            Assert.Contains("Unknown gateway action 'status'", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
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

    private sealed class DashboardCliOverrideScope : IDisposable
    {
        private readonly Func<Task<string?>> _getRunningGatewayBaseUrlAsync = DashboardCliHandler.GetRunningGatewayBaseUrlAsync;
        private readonly Func<int, Task<string?>> _waitForGatewayHealthyBaseUrlAsync = DashboardCliHandler.WaitForGatewayHealthyBaseUrlAsync;
        private readonly Func<DashboardCliHandler.IDaemonProcessHandle> _startDaemonProcess = DashboardCliHandler.StartDaemonProcess;
        private readonly Action<string> _openBrowser = DashboardCliHandler.OpenBrowser;

        public void Dispose()
        {
            DashboardCliHandler.GetRunningGatewayBaseUrlAsync = _getRunningGatewayBaseUrlAsync;
            DashboardCliHandler.WaitForGatewayHealthyBaseUrlAsync = _waitForGatewayHealthyBaseUrlAsync;
            DashboardCliHandler.StartDaemonProcess = _startDaemonProcess;
            DashboardCliHandler.OpenBrowser = _openBrowser;
        }
    }

    private sealed class FakeDaemonProcessHandle : DashboardCliHandler.IDaemonProcessHandle
    {
        public bool HasExited { get; set; }
        public bool KillCalled { get; private set; }
        public bool DisposeCalled { get; private set; }

        public void Kill() => KillCalled = true;

        public void Dispose() => DisposeCalled = true;
    }
}
