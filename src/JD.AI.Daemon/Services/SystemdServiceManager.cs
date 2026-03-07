using System.Runtime.Versioning;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Manages the JD.AI daemon as a systemd service on Linux.
/// Creates a systemd unit file and controls the service via systemctl.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class SystemdServiceManager : IServiceManager
{
    private const string ServiceName = DaemonServiceIdentity.LinuxServiceName;
    private const string UnitFileName = $"{ServiceName}.service";
    private const string UnitFilePath = $"/etc/systemd/system/{UnitFileName}";

    public async Task<ServiceResult> InstallAsync(CancellationToken ct = default)
    {
        var toolPath = GetToolPath();
        if (toolPath is null)
            return new ServiceResult(false, $"Cannot locate {DaemonServiceIdentity.ToolCommand}. Is it installed via 'dotnet tool install -g JD.AI.Daemon'?");

        var user = Environment.UserName;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataDir = Core.Config.DataDirectories.Root;

        var unitContent = $"""
            [Unit]
            Description={DaemonServiceIdentity.WindowsDisplayName}
            Documentation=https://jerrettdavis.github.io/JD.AI/
            After=network-online.target
            Wants=network-online.target

            [Service]
            Type=notify
            ExecStart={toolPath} run
            WorkingDirectory={home}
            User={user}
            Restart=on-failure
            RestartSec=5
            Environment=DOTNET_ENVIRONMENT=Production
            Environment=HOME={home}
            Environment=JDAI_DATA_DIR={dataDir}

            # Hardening
            ProtectSystem=strict
            ReadWritePaths={dataDir}
            PrivateTmp=true
            NoNewPrivileges=true

            [Install]
            WantedBy=multi-user.target
            """;

        try
        {
            await File.WriteAllTextAsync(UnitFilePath, unitContent, ct);
        }
        catch (UnauthorizedAccessException)
        {
            return new ServiceResult(false,
                $"Permission denied writing {UnitFilePath}. Run with sudo:\n  sudo {DaemonServiceIdentity.ToolCommand} install");
        }

        await RunSystemctlAsync("daemon-reload", ct);
        await RunSystemctlAsync($"enable {ServiceName}", ct);

        return new ServiceResult(true,
            $"Service '{ServiceName}' installed and enabled. Run '{DaemonServiceIdentity.ToolCommand} start' to begin.");
    }

    public async Task<ServiceResult> UninstallAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (status.State == ServiceState.Running)
            await StopAsync(ct);

        await RunSystemctlAsync($"disable {ServiceName}", ct);

        try
        {
            if (File.Exists(UnitFilePath))
                File.Delete(UnitFilePath);
        }
        catch (UnauthorizedAccessException)
        {
            return new ServiceResult(false,
                $"Permission denied deleting {UnitFilePath}. Run with sudo:\n  sudo {DaemonServiceIdentity.ToolCommand} uninstall");
        }

        await RunSystemctlAsync("daemon-reload", ct);
        return new ServiceResult(true, $"Service '{ServiceName}' uninstalled.");
    }

    public async Task<ServiceResult> StartAsync(CancellationToken ct = default)
    {
        var (exitCode, output) = await RunSystemctlAsync($"start {ServiceName}", ct);
        return exitCode == 0
            ? new ServiceResult(true, $"Service '{ServiceName}' started.")
            : new ServiceResult(false, $"Failed to start: {output}");
    }

    public async Task<ServiceResult> StopAsync(CancellationToken ct = default)
    {
        var (exitCode, output) = await RunSystemctlAsync($"stop {ServiceName}", ct);
        return exitCode == 0
            ? new ServiceResult(true, $"Service '{ServiceName}' stopped.")
            : new ServiceResult(false, $"Failed to stop: {output}");
    }

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (!File.Exists(UnitFilePath))
            return new ServiceStatus(ServiceState.NotInstalled, null, null, "Unit file not found.");

        var (exitCode, output) = await RunSystemctlAsync($"is-active {ServiceName}", ct);
        var activeState = output.Trim();

        var state = activeState switch
        {
            "active" => ServiceState.Running,
            "inactive" => ServiceState.Stopped,
            "activating" => ServiceState.Starting,
            "deactivating" => ServiceState.Stopping,
            _ => ServiceState.Unknown,
        };

        // Get detailed status
        var (_, details) = await RunSystemctlAsync($"status {ServiceName} --no-pager", ct);
        var version = typeof(SystemdServiceManager).Assembly.GetName().Version?.ToString() ?? "unknown";

        return new ServiceStatus(state, version, null, details.Trim());
    }

    public async Task<ServiceResult> ShowLogsAsync(int lines = 50, CancellationToken ct = default)
    {
        var (exitCode, output) = await RunProcessAsync(
            "journalctl", $"-u {ServiceName} -n {lines} --no-pager", ct);

        return exitCode == 0
            ? new ServiceResult(true, output)
            : new ServiceResult(false, $"Failed to read logs: {output}");
    }

    private static string? GetToolPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolPath = Path.Combine(home, ".dotnet", "tools", DaemonServiceIdentity.ToolCommand);
        return File.Exists(toolPath) ? toolPath : null;
    }

    private static Task<(int ExitCode, string Output)> RunSystemctlAsync(string arguments, CancellationToken ct)
        => RunProcessAsync("systemctl", arguments, ct);

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string arguments, CancellationToken ct)
    {
        var result = await ProcessExecutor.RunAsync(
            fileName, arguments, cancellationToken: ct);

        var output = string.IsNullOrEmpty(result.StandardError)
            ? result.StandardOutput
            : $"{result.StandardOutput}\n{result.StandardError}";
        return (result.ExitCode, output);
    }
}
