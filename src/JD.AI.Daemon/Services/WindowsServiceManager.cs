using System.Runtime.Versioning;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Manages the JD.AI daemon as a Windows Service using sc.exe.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsServiceManager : IServiceManager
{
    private const string ServiceName = "JDAIDaemon";
    private const string DisplayName = "JD.AI Gateway Daemon";
    private const string Description = "JD.AI AI Gateway — manages AI agents, channels, and routing.";

    public async Task<ServiceResult> InstallAsync(CancellationToken ct = default)
    {
        var toolPath = GetToolPath();
        if (toolPath is null)
            return new ServiceResult(false, "Cannot locate jdai-daemon executable. Is it installed as a dotnet tool?");

        // Create the service
        var (exitCode, output) = await RunScAsync(
            $"create {ServiceName} binPath=\"{toolPath} run\" start=auto DisplayName=\"{DisplayName}\"", ct);

        if (exitCode != 0)
            return new ServiceResult(false, $"sc create failed: {output}");

        // Set description
        await RunScAsync($"description {ServiceName} \"{Description}\"", ct);

        // Configure recovery: restart on failure
        await RunScAsync($"failure {ServiceName} reset=86400 actions=restart/5000/restart/10000/restart/30000", ct);

        return new ServiceResult(true, $"Service '{ServiceName}' installed. Run 'jdai-daemon start' to begin.");
    }

    public async Task<ServiceResult> UninstallAsync(CancellationToken ct = default)
    {
        // Stop first if running
        var status = await GetStatusAsync(ct);
        if (status.State == ServiceState.Running)
            await StopAsync(ct);

        var (exitCode, output) = await RunScAsync($"delete {ServiceName}", ct);
        return exitCode == 0
            ? new ServiceResult(true, $"Service '{ServiceName}' uninstalled.")
            : new ServiceResult(false, $"sc delete failed: {output}");
    }

    public async Task<ServiceResult> StartAsync(CancellationToken ct = default)
    {
        var (exitCode, output) = await RunScAsync($"start {ServiceName}", ct);
        return exitCode == 0
            ? new ServiceResult(true, $"Service '{ServiceName}' started.")
            : new ServiceResult(false, $"sc start failed: {output}");
    }

    public async Task<ServiceResult> StopAsync(CancellationToken ct = default)
    {
        var (exitCode, output) = await RunScAsync($"stop {ServiceName}", ct);
        return exitCode == 0
            ? new ServiceResult(true, $"Service '{ServiceName}' stopped.")
            : new ServiceResult(false, $"sc stop failed: {output}");
    }

    public async Task<ServiceStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var (exitCode, output) = await RunScAsync($"query {ServiceName}", ct);

        if (exitCode != 0 || output.Contains("FAILED 1060", StringComparison.Ordinal))
            return new ServiceStatus(ServiceState.NotInstalled, null, null, "Service is not installed.");

        var state = output switch
        {
            _ when output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) => ServiceState.Running,
            _ when output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase) => ServiceState.Stopped,
            _ when output.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase) => ServiceState.Starting,
            _ when output.Contains("STOP_PENDING", StringComparison.OrdinalIgnoreCase) => ServiceState.Stopping,
            _ => ServiceState.Unknown,
        };

        var version = typeof(WindowsServiceManager).Assembly.GetName().Version?.ToString() ?? "unknown";
        return new ServiceStatus(state, version, null, output.Trim());
    }

    public async Task<ServiceResult> ShowLogsAsync(int lines = 50, CancellationToken ct = default)
    {
        // Read from Windows Event Log
        var (exitCode, output) = await RunProcessAsync(
            "powershell",
            $"-NoProfile -Command \"Get-EventLog -LogName Application -Source '{ServiceName}' -Newest {lines} | Format-Table -AutoSize\"",
            ct);

        return exitCode == 0
            ? new ServiceResult(true, output)
            : new ServiceResult(true, "No event log entries found. The service may use file-based logging — check ~/.jdai/logs/.");
    }

    private static string? GetToolPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolPath = Path.Combine(home, ".dotnet", "tools", "jdai-daemon.exe");
        return File.Exists(toolPath) ? toolPath : null;
    }

    private static async Task<(int ExitCode, string Output)> RunScAsync(string arguments, CancellationToken ct)
        => await RunProcessAsync("sc.exe", arguments, ct);

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
