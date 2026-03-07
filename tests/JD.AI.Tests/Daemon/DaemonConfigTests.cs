using JD.AI.Daemon.Config;
using JD.AI.Daemon.Services;

namespace JD.AI.Tests.Daemon;

/// <summary>Tests for UpdateConfig defaults and ServiceResult/ServiceStatus records.</summary>
public sealed class DaemonConfigTests
{
    [Fact]
    public void UpdateConfig_Defaults_AreCorrect()
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
    public void ServiceResult_Properties_AreSet()
    {
        var result = new ServiceResult(true, "OK");
        Assert.True(result.Success);
        Assert.Equal("OK", result.Message);
    }

    [Fact]
    public void ServiceResult_WithDeconstruct_Works()
    {
        var (success, message) = new ServiceResult(false, "Failed");
        Assert.False(success);
        Assert.Equal("Failed", message);
    }

    [Fact]
    public void ServiceStatus_Properties_AreSet()
    {
        var uptime = TimeSpan.FromMinutes(5);
        var status = new ServiceStatus(ServiceState.Running, "1.0.0", uptime, "All good");
        Assert.Equal(ServiceState.Running, status.State);
        Assert.Equal("1.0.0", status.Version);
        Assert.Equal(uptime, status.Uptime);
        Assert.Equal("All good", status.Details);
    }

    [Fact]
    public void ServiceState_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(ServiceState.Unknown));
        Assert.True(Enum.IsDefined(ServiceState.NotInstalled));
        Assert.True(Enum.IsDefined(ServiceState.Stopped));
        Assert.True(Enum.IsDefined(ServiceState.Running));
        Assert.True(Enum.IsDefined(ServiceState.Starting));
        Assert.True(Enum.IsDefined(ServiceState.Stopping));
    }
}
