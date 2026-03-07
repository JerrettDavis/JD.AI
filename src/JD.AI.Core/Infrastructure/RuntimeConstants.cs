namespace JD.AI.Core.Infrastructure;

/// <summary>
/// Shared runtime defaults for gateway host/port/addressing and health endpoints.
/// </summary>
public static class GatewayRuntimeDefaults
{
    public const string DefaultHost = "localhost";
    public const int DefaultPort = 15790;

    public const string HealthPath = "/health";
    public const string HealthReadyPath = "/health/ready";
    public const string HealthLivePath = "/health/live";
    public const string HealthStartupPath = "/health/startup";
    public const string ReadyPath = "/ready";
}

/// <summary>
/// Shared service and CLI identity constants for the JD.AI daemon.
/// </summary>
public static class DaemonServiceIdentity
{
    public const string ToolCommand = "jdai-daemon";
    public const string WindowsServiceName = "JDAIDaemon";
    public const string LinuxServiceName = ToolCommand;

    public const string WindowsDisplayName = "JD.AI Gateway Daemon";
    public const string WindowsDescription = "JD.AI AI Gateway - manages AI agents, channels, and routing.";
    public const string HostedServiceDisplayName = "JD.AI Gateway";
}
