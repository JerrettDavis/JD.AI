using JD.AI.Daemon.Services;

namespace JD.AI.Tests.Daemon.Services;

public sealed class ServiceRestartCoordinatorTests
{
    [Fact]
    public async Task RestartAsync_WhenServiceNotInstalled_ReturnsFailureWithoutStoppingOrStarting()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.NotInstalled, null, null, null)
        };

        var result = await ServiceRestartCoordinator.RestartAsync(manager);

        Assert.False(result.Success);
        Assert.Equal("Service is not installed.", result.Message);
        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(0, manager.StopCalls);
        Assert.Equal(0, manager.StartCalls);
    }

    [Fact]
    public async Task RestartAsync_WhenServiceIsRunning_StopsThenStarts()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Running, null, null, null)
        };

        var result = await ServiceRestartCoordinator.RestartAsync(manager);

        Assert.True(result.Success);
        Assert.Equal("Service restarted.", result.Message);
        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(1, manager.StopCalls);
        Assert.Equal(1, manager.StartCalls);
    }

    [Fact]
    public async Task RestartAsync_WhenServiceIsStopped_StartsWithoutStopping()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Stopped, null, null, null)
        };

        var result = await ServiceRestartCoordinator.RestartAsync(manager);

        Assert.True(result.Success);
        Assert.Equal("Service restarted.", result.Message);
        Assert.Equal(1, manager.GetStatusCalls);
        Assert.Equal(0, manager.StopCalls);
        Assert.Equal(1, manager.StartCalls);
    }

    [Fact]
    public async Task RestartAsync_WhenStopFails_ReturnsFailureWithoutStarting()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Running, null, null, null),
            StopToReturn = new ServiceResult(false, "stop failed")
        };

        var result = await ServiceRestartCoordinator.RestartAsync(manager);

        Assert.False(result.Success);
        Assert.Equal("Failed to stop service before restart: stop failed", result.Message);
        Assert.Equal(1, manager.StopCalls);
        Assert.Equal(0, manager.StartCalls);
    }

    [Fact]
    public async Task RestartAsync_WhenStartFails_ReturnsFailure()
    {
        var manager = new RecordingServiceManager
        {
            StatusToReturn = new ServiceStatus(ServiceState.Stopped, null, null, null),
            StartToReturn = new ServiceResult(false, "start failed")
        };

        var result = await ServiceRestartCoordinator.RestartAsync(manager);

        Assert.False(result.Success);
        Assert.Equal("Failed to start service after restart: start failed", result.Message);
        Assert.Equal(0, manager.StopCalls);
        Assert.Equal(1, manager.StartCalls);
    }

    private sealed class RecordingServiceManager : IServiceManager
    {
        public int GetStatusCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int StartCalls { get; private set; }

        public ServiceStatus StatusToReturn { get; set; } = new(ServiceState.Stopped, null, null, null);
        public ServiceResult StopToReturn { get; set; } = new(true, "stopped");
        public ServiceResult StartToReturn { get; set; } = new(true, "started");

        public Task<ServiceResult> InstallAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> UninstallAsync(CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));

        public Task<ServiceResult> StartAsync(CancellationToken ct = default)
        {
            StartCalls++;
            return Task.FromResult(StartToReturn);
        }

        public Task<ServiceResult> StopAsync(CancellationToken ct = default)
        {
            StopCalls++;
            return Task.FromResult(StopToReturn);
        }

        public Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default)
        {
            GetStatusCalls++;
            return Task.FromResult(StatusToReturn);
        }

        public Task<ServiceResult> ShowLogsAsync(int lines = 50, CancellationToken ct = default) =>
            Task.FromResult(new ServiceResult(true, "ok"));
    }
}
