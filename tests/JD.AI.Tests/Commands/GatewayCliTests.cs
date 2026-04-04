using JD.AI.Commands;
using JD.AI.Core.Infrastructure;
using JD.AI.Utilities;
using System.Reflection;

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

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenGatewayAlreadyRunning_SkipsDaemonStart()
    {
        var started = false;

        var exitCode = await GatewayCliHandler.RunStartAsync(
            null,
            isRunningAsync: _ => Task.FromResult(true),
            startDaemonProcess: () =>
            {
                started = true;
                return new FakeGatewayDaemonProcessHandle();
            });

        Assert.Equal(0, exitCode);
        Assert.False(started);
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenDaemonStartFails_ReturnsFailure()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await GatewayCliHandler.RunStartAsync(
                null,
                isRunningAsync: _ => Task.FromResult(false),
                startDaemonProcess: () => throw new InvalidOperationException("boom"));

            Assert.Equal(1, exitCode);
            Assert.Contains("Failed to start daemon: boom", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenGatewayDoesNotBecomeHealthy_KillsAndDisposesProcess()
    {
        var process = new FakeGatewayDaemonProcessHandle();

        var exitCode = await GatewayCliHandler.RunStartAsync(
            null,
            isRunningAsync: _ => Task.FromResult(false),
            waitForHealthyAsync: (_, _) => Task.FromResult(false),
            startDaemonProcess: () => process);

        Assert.Equal(1, exitCode);
        Assert.True(process.KillCalled);
        Assert.True(process.DisposeCalled);
        Assert.Equal(2, process.WaitForExitCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenGatewayBecomesHealthy_StopsProcessOnShutdown()
    {
        var process = new FakeGatewayDaemonProcessHandle();
        var shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var exitCode = await GatewayCliHandler.RunStartAsync(
            null,
            isRunningAsync: _ => Task.FromResult(false),
            waitForHealthyAsync: (_, _) =>
            {
                _ = Task.Run(() => shutdown.TrySetResult(true));
                return Task.FromResult(true);
            },
            startDaemonProcess: () => process,
            waitForShutdownAsync: _ => shutdown.Task);

        Assert.Equal(0, exitCode);
        Assert.True(process.KillCalled);
        Assert.True(process.DisposeCalled);
        Assert.Equal(2, process.WaitForExitCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenShutdownSeesExitedProcess_DisposesWithoutKilling()
    {
        var process = new FakeGatewayDaemonProcessHandle();
        var shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var exitCode = await GatewayCliHandler.RunStartAsync(
            null,
            isRunningAsync: _ => Task.FromResult(false),
            waitForHealthyAsync: (_, _) =>
            {
                _ = Task.Run(() =>
                {
                    process.HasExited = true;
                    shutdown.TrySetResult(true);
                });
                return Task.FromResult(true);
            },
            startDaemonProcess: () => process,
            waitForShutdownAsync: _ => shutdown.Task);

        Assert.Equal(0, exitCode);
        Assert.False(process.KillCalled);
        Assert.True(process.DisposeCalled);
        Assert.Equal(1, process.WaitForExitCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenShutdownOccursBeforeHealthy_CleansUpDaemon()
    {
        var process = new FakeGatewayDaemonProcessHandle();
        var healthy = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var runTask = GatewayCliHandler.RunStartAsync(
            null,
            isRunningAsync: _ => Task.FromResult(false),
            waitForHealthyAsync: (_, _) => healthy.Task,
            startDaemonProcess: () => process,
            waitForShutdownAsync: _ => shutdown.Task);

        shutdown.TrySetResult(true);

        var exitCode = await runTask;

        Assert.Equal(0, exitCode);
        Assert.True(process.KillCalled);
        Assert.True(process.DisposeCalled);
        Assert.Equal(2, process.WaitForExitCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenBaseUrlIsNull_UsesDefaultHealthCheckFallback()
    {
        string? observedBaseUrl = "unset";

        var exitCode = await GatewayCliHandler.RunStartAsync(
            null,
            isRunningAsync: baseUrl =>
            {
                observedBaseUrl = baseUrl;
                return Task.FromResult(true);
            });

        Assert.Equal(0, exitCode);
        Assert.Null(observedBaseUrl);
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenDaemonExitsUnexpectedly_ReturnsFailure()
    {
        var process = new FakeGatewayDaemonProcessHandle();
        process.CompleteExit();

        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await GatewayCliHandler.RunStartAsync(
                null,
                isRunningAsync: _ => Task.FromResult(false),
                waitForHealthyAsync: (_, _) => Task.FromResult(true),
                startDaemonProcess: () => process,
                waitForShutdownAsync: _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task);

            Assert.Equal(1, exitCode);
            Assert.Contains("Gateway exited unexpectedly.", writer.ToString(), StringComparison.Ordinal);
            Assert.False(process.KillCalled);
            Assert.True(process.DisposeCalled);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task GatewayCliHandler_RunStartAsync_WhenDaemonExitsBeforeHealthy_ReturnsFailure()
    {
        var process = new FakeGatewayDaemonProcessHandle();
        process.CompleteExit();

        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await GatewayCliHandler.RunStartAsync(
                null,
                isRunningAsync: _ => Task.FromResult(false),
                waitForHealthyAsync: (_, _) => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task,
                startDaemonProcess: () => process);

            Assert.Equal(1, exitCode);
            Assert.Contains("Gateway exited before becoming healthy.", writer.ToString(), StringComparison.Ordinal);
            Assert.False(process.KillCalled);
            Assert.True(process.DisposeCalled);
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
    public async Task GatewayCliHandler_RunAsync_PublicOverload_WhenActionIsUnknown_WritesUsageError()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = await GatewayCliHandler.RunAsync(["status"]);

            Assert.Equal(1, exitCode);
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

    private sealed class FakeGatewayDaemonProcessHandle : GatewayCliHandler.IDaemonProcessHandle
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasExited { get; set; }
        public bool KillCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public int WaitForExitCalls { get; private set; }

        public void Kill()
        {
            KillCalled = true;
            HasExited = true;
            _exit.TrySetResult();
        }

        public Task WaitForExitAsync()
        {
            WaitForExitCalls++;
            return HasExited ? Task.CompletedTask : _exit.Task;
        }

        public void Dispose() => DisposeCalled = true;

        public void CompleteExit()
        {
            HasExited = true;
            _exit.TrySetResult();
        }
    }
}

[Collection("EnvironmentVariables")]
public sealed class GatewayCliPrivateTests
{
    [Fact]
    public async Task GatewayCliHandler_CleanupDaemonProcessAsync_WhenKillThrows_StillDisposes()
    {
        var handle = new ThrowingGatewayDaemonProcessHandle();

        await InvokePrivateTaskAsync("CleanupDaemonProcessAsync", handle);

        Assert.True(handle.KillCalled);
        Assert.True(handle.DisposeCalled);
        Assert.Equal(0, handle.WaitForExitCalls);
    }

    [Fact]
    public async Task GatewayCliHandler_RunDaemonCommandAsync_WhenDaemonCommandIsMissing_ReturnsGuidance()
    {
        using var scope = new PathOverrideScope(string.Empty);

        var result = await InvokePrivateTaskWithResultAsync<GatewayCliHandler.DaemonCommandResult>(
            "RunDaemonCommandAsync",
            "status");

        Assert.False(result.Success);
        Assert.Contains("Failed to run daemon command 'status'", result.Output, StringComparison.Ordinal);
        Assert.Contains("Ensure 'jdai-daemon' is installed", result.Output, StringComparison.Ordinal);
    }

    private static async Task InvokePrivateTaskAsync(string methodName, params object?[] args)
    {
        var method = typeof(GatewayCliHandler).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(GatewayCliHandler.IDaemonProcessHandle)],
            modifiers: null);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method.Invoke(null, args));
        await task;
    }

    private static async Task<T> InvokePrivateTaskWithResultAsync<T>(string methodName, params object?[] args)
    {
        var method = typeof(GatewayCliHandler).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task<T>>(method.Invoke(null, args));
        return await task;
    }

    private sealed class PathOverrideScope : IDisposable
    {
        private readonly string? _originalPath = Environment.GetEnvironmentVariable("PATH");

        public PathOverrideScope(string commandDirectory)
        {
            var newPath = string.IsNullOrWhiteSpace(_originalPath)
                ? commandDirectory
                : string.IsNullOrWhiteSpace(commandDirectory)
                    ? string.Empty
                    : string.Join(Path.PathSeparator, commandDirectory, _originalPath);
            Environment.SetEnvironmentVariable("PATH", newPath);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("PATH", _originalPath);
        }
    }

    private sealed class ThrowingGatewayDaemonProcessHandle : GatewayCliHandler.IDaemonProcessHandle
    {
        public bool HasExited => false;
        public bool KillCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public int WaitForExitCalls { get; private set; }

        public void Kill()
        {
            KillCalled = true;
            throw new InvalidOperationException("already exited");
        }

        public Task WaitForExitAsync()
        {
            WaitForExitCalls++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }
}
