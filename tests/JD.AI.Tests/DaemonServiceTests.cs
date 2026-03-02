using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;
using Microsoft.Extensions.DependencyInjection;
using DaemonUpdateChecker = JD.AI.Daemon.Services.UpdateChecker;
using DaemonUpdateInfo = JD.AI.Daemon.Services.UpdateInfo;

namespace JD.AI.Tests;

public sealed class DaemonServiceTests
{
    // ── UpdateConfig defaults ──────────────────────────────────────
    [Fact]
    public void UpdateConfig_HasSensibleDefaults()
    {
        var config = new UpdateConfig();

        Assert.Equal(TimeSpan.FromHours(24), config.CheckInterval);
        Assert.False(config.AutoApply);
        Assert.True(config.NotifyChannels);
        Assert.False(config.PreRelease);
        Assert.Equal(TimeSpan.FromSeconds(30), config.DrainTimeout);
        Assert.Equal("JD.AI.Daemon", config.PackageId);
        Assert.Contains("nuget.org", config.NuGetFeedUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateConfig_AllPropertiesSettable()
    {
        var config = new UpdateConfig
        {
            CheckInterval = TimeSpan.FromMinutes(30),
            AutoApply = true,
            NotifyChannels = false,
            PreRelease = true,
            DrainTimeout = TimeSpan.FromSeconds(60),
            PackageId = "Custom.Package",
            NuGetFeedUrl = "https://custom.feed/v3/",
        };

        Assert.Equal(TimeSpan.FromMinutes(30), config.CheckInterval);
        Assert.True(config.AutoApply);
        Assert.False(config.NotifyChannels);
        Assert.True(config.PreRelease);
        Assert.Equal(TimeSpan.FromSeconds(60), config.DrainTimeout);
        Assert.Equal("Custom.Package", config.PackageId);
        Assert.Equal("https://custom.feed/v3/", config.NuGetFeedUrl);
    }

    // ── IServiceManager interface ──────────────────────────────────
    [Fact]
    public void ServiceResult_Properties()
    {
        var success = new ServiceResult(true, "Installed");
        var failure = new ServiceResult(false, "Permission denied");

        Assert.True(success.Success);
        Assert.Equal("Installed", success.Message);
        Assert.False(failure.Success);
    }

    [Fact]
    public void ServiceStatus_Properties()
    {
        var status = new ServiceStatus(
            ServiceState.Running,
            "1.0.0",
            TimeSpan.FromHours(2),
            "PID: 1234");

        Assert.Equal(ServiceState.Running, status.State);
        Assert.Equal("1.0.0", status.Version);
        Assert.Equal(TimeSpan.FromHours(2), status.Uptime);
        Assert.Equal("PID: 1234", status.Details);
    }

    [Fact]
    public void ServiceStatus_NotInstalled()
    {
        var status = new ServiceStatus(ServiceState.NotInstalled, null, null, null);

        Assert.Equal(ServiceState.NotInstalled, status.State);
        Assert.Null(status.Version);
        Assert.Null(status.Uptime);
    }

    [Theory]
    [InlineData(ServiceState.Unknown)]
    [InlineData(ServiceState.NotInstalled)]
    [InlineData(ServiceState.Stopped)]
    [InlineData(ServiceState.Running)]
    [InlineData(ServiceState.Starting)]
    [InlineData(ServiceState.Stopping)]
    public void ServiceState_HasAllExpectedValues(ServiceState state)
    {
        Assert.True(Enum.IsDefined(state));
    }

    // ── UpdateInfo record ──────────────────────────────────────────
    [Fact]
    public void UpdateInfo_FormatsToString()
    {
        var info = new DaemonUpdateInfo(new Version(1, 0, 0), new Version(2, 0, 0));
        Assert.Contains("1.0.0", info.ToString(), StringComparison.Ordinal);
        Assert.Contains("2.0.0", info.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateInfo_Equality()
    {
        var a = new DaemonUpdateInfo(new Version(1, 0, 0), new Version(2, 0, 0));
        var b = new DaemonUpdateInfo(new Version(1, 0, 0), new Version(2, 0, 0));
        Assert.Equal(a, b);
    }

    // ── Platform ServiceManager factory ────────────────────────────
    [Fact]
    public void ServiceManager_CanBeCreatedForCurrentPlatform()
    {
        // Verify we can detect the current platform without throwing
        if (OperatingSystem.IsWindows())
        {
            var mgr = new WindowsServiceManager();
            Assert.NotNull(mgr);
        }
        else if (OperatingSystem.IsLinux())
        {
            var mgr = new SystemdServiceManager();
            Assert.NotNull(mgr);
        }
        else
        {
            // macOS or other — should not crash, just skip
            Assert.True(true);
        }
    }

    [Fact]
    public async Task WindowsServiceManager_Status_ReturnsNotInstalled_WhenServiceDoesNotExist()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var mgr = new WindowsServiceManager();
        var status = await mgr.GetStatusAsync();

        // The test service isn't installed, so we expect NotInstalled or Unknown
        Assert.True(
            status.State is ServiceState.NotInstalled or ServiceState.Unknown,
            $"Expected NotInstalled or Unknown, got {status.State}");
    }

    // ── UpdateChecker version comparison ───────────────────────────
    [Fact]
    public void UpdateChecker_CurrentVersion_ReturnsNonNull()
    {
        var services = new ServiceCollection();
        services.Configure<UpdateConfig>(_ => { });
        services.AddHttpClient("NuGet");
        services.AddLogging();
        services.AddSingleton<DaemonUpdateChecker>();
        using var sp = services.BuildServiceProvider();

        var checker = sp.GetRequiredService<DaemonUpdateChecker>();
        Assert.NotNull(checker.CurrentVersion);
    }

    [Fact]
    public async Task UpdateChecker_CheckForUpdate_HandlesInvalidFeedGracefully()
    {
        var services = new ServiceCollection();
        services.Configure<UpdateConfig>(c => c.NuGetFeedUrl = "https://invalid.example.com/");
        services.AddHttpClient("NuGet");
        services.AddLogging();
        services.AddSingleton<DaemonUpdateChecker>();
        using var sp = services.BuildServiceProvider();

        var checker = sp.GetRequiredService<DaemonUpdateChecker>();
        var result = await checker.CheckForUpdateAsync();

        // Should return null gracefully, not throw
        Assert.Null(result);
    }

    // ── UpdateService draining state ───────────────────────────────
    [Fact]
    public void UpdateService_InitialState_NotDraining()
    {
        var services = new ServiceCollection();
        services.Configure<UpdateConfig>(_ => { });
        services.AddHttpClient("NuGet");
        services.AddLogging();
        services.AddSingleton<DaemonUpdateChecker>();
        services.AddSingleton<JD.AI.Core.Events.IEventBus, JD.AI.Core.Events.InProcessEventBus>();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostApplicationLifetime>(
            NSubstitute.Substitute.For<Microsoft.Extensions.Hosting.IHostApplicationLifetime>());
        services.AddSingleton<UpdateService>();
        using var sp = services.BuildServiceProvider();

        var svc = sp.GetRequiredService<UpdateService>();
        Assert.False(svc.IsDraining);
        Assert.Null(svc.PendingUpdate);
    }

    // ── ServiceResult record equality ──────────────────────────────
    [Fact]
    public void ServiceResult_RecordEquality()
    {
        var a = new ServiceResult(true, "OK");
        var b = new ServiceResult(true, "OK");
        Assert.Equal(a, b);
        Assert.NotEqual(a, new ServiceResult(false, "OK"));
    }

    [Fact]
    public void ServiceStatus_RecordEquality()
    {
        var a = new ServiceStatus(ServiceState.Running, "1.0.0", null, null);
        var b = new ServiceStatus(ServiceState.Running, "1.0.0", null, null);
        Assert.Equal(a, b);
    }

    // ── Windows ServiceManager logs ────────────────────────────────
    [Fact]
    public async Task WindowsServiceManager_ShowLogs_DoesNotThrow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var mgr = new WindowsServiceManager();
        var result = await mgr.ShowLogsAsync(10);

        // Should return a result regardless of whether event log has entries
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
    }
}
