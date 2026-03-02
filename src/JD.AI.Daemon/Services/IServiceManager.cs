namespace JD.AI.Daemon.Services;

/// <summary>
/// Abstraction for platform-specific service lifecycle management.
/// </summary>
public interface IServiceManager
{
    /// <summary>Installs the daemon as a system service.</summary>
    Task<ServiceResult> InstallAsync(CancellationToken ct = default);

    /// <summary>Uninstalls the system service.</summary>
    Task<ServiceResult> UninstallAsync(CancellationToken ct = default);

    /// <summary>Starts the installed service.</summary>
    Task<ServiceResult> StartAsync(CancellationToken ct = default);

    /// <summary>Stops the running service.</summary>
    Task<ServiceResult> StopAsync(CancellationToken ct = default);

    /// <summary>Gets the current service status.</summary>
    Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Streams recent service logs to the console.</summary>
    Task<ServiceResult> ShowLogsAsync(int lines = 50, CancellationToken ct = default);
}

/// <summary>Result of a service management operation.</summary>
public record ServiceResult(bool Success, string Message);

/// <summary>Current service status.</summary>
public record ServiceStatus(
    ServiceState State,
    string? Version,
    TimeSpan? Uptime,
    string? Details);

/// <summary>Possible service states.</summary>
public enum ServiceState
{
    Unknown,
    NotInstalled,
    Stopped,
    Running,
    Starting,
    Stopping,
}
