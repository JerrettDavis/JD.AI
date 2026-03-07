using JD.AI.Core.Infrastructure;

namespace JD.AI.Tests.Infrastructure;

public sealed class RuntimeConstantsTests
{
    [Fact]
    public void GatewayRuntimeDefaults_AreStable()
    {
        Assert.Equal("localhost", GatewayRuntimeDefaults.DefaultHost);
        Assert.Equal(15790, GatewayRuntimeDefaults.DefaultPort);
        Assert.Equal("/health", GatewayRuntimeDefaults.HealthPath);
        Assert.Equal("/health/ready", GatewayRuntimeDefaults.HealthReadyPath);
        Assert.Equal("/health/live", GatewayRuntimeDefaults.HealthLivePath);
        Assert.Equal("/health/startup", GatewayRuntimeDefaults.HealthStartupPath);
        Assert.Equal("/ready", GatewayRuntimeDefaults.ReadyPath);
    }

    [Fact]
    public void DaemonServiceIdentity_UsesSingleToolCommandForLinux()
    {
        Assert.Equal("jdai-daemon", DaemonServiceIdentity.ToolCommand);
        Assert.Equal(DaemonServiceIdentity.ToolCommand, DaemonServiceIdentity.LinuxServiceName);
        Assert.Equal("JDAIDaemon", DaemonServiceIdentity.WindowsServiceName);
    }
}
