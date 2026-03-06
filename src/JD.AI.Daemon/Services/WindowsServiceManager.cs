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
    private const string Description = "JD.AI AI Gateway - manages AI agents, channels, and routing.";

    private const string OpenClawTaskName = "\\OpenClaw Gateway";
    private const string OpenClawWatchdogTaskName = "\\OpenClaw Gateway Watchdog";
    private const string OpenClawStateDirName = ".openclaw";
    private const string OpenClawGatewayScriptFile = "gateway-service.cmd";
    private const string OpenClawWatchdogScriptFile = "gateway-watchdog.cmd";
    private const int OpenClawGatewayPort = 18789;

    public async Task<ServiceResult> InstallAsync(CancellationToken ct = default)
    {
        var toolPath = GetToolPath();
        if (toolPath is null)
            return new ServiceResult(false, "Cannot locate jdai-daemon executable. Is it installed as a dotnet tool?");

        var serviceBinPath = BuildServiceBinPath(toolPath);
        var serviceExists = await ServiceExistsAsync(ct);

        var (exitCode, output) = serviceExists
            ? await RunScAsync($"config {ServiceName} binPath= {serviceBinPath} start= auto DisplayName= \"{DisplayName}\"", ct)
            : await RunScAsync($"create {ServiceName} binPath= {serviceBinPath} start= auto DisplayName= \"{DisplayName}\"", ct);

        if (exitCode != 0)
        {
            var verb = serviceExists ? "config" : "create";
            return new ServiceResult(false, $"sc {verb} failed: {output}");
        }

        // Best effort metadata configuration
        await RunScAsync($"description {ServiceName} \"{Description}\"", ct);
        await RunScAsync($"failure {ServiceName} reset=86400 actions=restart/5000/restart/10000/restart/30000", ct);

        var taskProvisioning = await EnsureOpenClawGatewayTasksAsync(ct);
        if (!taskProvisioning.Success)
            return taskProvisioning;

        var action = serviceExists ? "updated" : "installed";
        return new ServiceResult(true,
            $"Service '{ServiceName}' {action}. OpenClaw gateway tasks are configured. Run 'jdai-daemon start' to begin.");
    }

    public async Task<ServiceResult> UninstallAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (status.State == ServiceState.Running)
            await StopAsync(ct);

        if (status.State != ServiceState.NotInstalled)
        {
            var (exitCode, output) = await RunScAsync($"delete {ServiceName}", ct);
            if (exitCode != 0)
                return new ServiceResult(false, $"sc delete failed: {output}");
        }

        var cleanupResult = await CleanupOpenClawGatewayArtifactsAsync(ct);
        if (!cleanupResult.Success)
            return cleanupResult;

        return new ServiceResult(true, $"Service '{ServiceName}' uninstalled and OpenClaw gateway tasks removed.");
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
            : new ServiceResult(true, "No event log entries found. The service may use file-based logging - check ~/.jdai/logs/.");
    }

    internal static string BuildOpenClawGatewayScript(
        string stateDir,
        string configPath,
        string userHome,
        string openClawMainPath,
        int port = OpenClawGatewayPort)
    {
        return $"""
            @echo off
            setlocal

            set "OPENCLAW_STATE_DIR={stateDir}"
            set "OPENCLAW_CONFIG_PATH={configPath}"
            set "USERPROFILE={userHome}"
            set "HOME={userHome}"

            set "OPENCLAW_MAIN={openClawMainPath}"
            if not exist "%OPENCLAW_MAIN%" (
              echo OpenClaw entrypoint not found: %OPENCLAW_MAIN%
              exit /b 1
            )

            set "NODE_EXE=%ProgramFiles%\nodejs\node.exe"
            if exist "%NODE_EXE%" goto run_gateway

            for %%I in (node.exe) do set "NODE_EXE=%%~$PATH:I"
            if not defined NODE_EXE (
              echo node.exe not found. Install Node.js and openclaw.
              exit /b 1
            )

            :run_gateway
            "%NODE_EXE%" "%OPENCLAW_MAIN%" gateway --port {port}
            """;
    }

    internal static string BuildOpenClawWatchdogScript(string gatewayTaskName = OpenClawTaskName)
    {
        return $"""
            @echo off
            schtasks /run /tn "{gatewayTaskName}" >nul 2>&1
            """;
    }

    private static string? GetToolPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolPath = Path.Combine(home, ".dotnet", "tools", "jdai-daemon.exe");
        return File.Exists(toolPath) ? toolPath : null;
    }

    private static string BuildServiceBinPath(string toolPath)
        => $"\"{toolPath}\" run";

    private static async Task<bool> ServiceExistsAsync(CancellationToken ct)
    {
        var (exitCode, output) = await RunScAsync($"query {ServiceName}", ct);
        return exitCode == 0 && !output.Contains("FAILED 1060", StringComparison.Ordinal);
    }

    private static async Task<ServiceResult> EnsureOpenClawGatewayTasksAsync(CancellationToken ct)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            return new ServiceResult(false, "Cannot resolve user profile path for OpenClaw task setup.");

        var stateDir = Path.Combine(home, OpenClawStateDirName);
        Directory.CreateDirectory(stateDir);

        var configPath = Path.Combine(stateDir, "openclaw.json");
        var openClawMainPath = Path.Combine(home, "AppData", "Roaming", "npm", "node_modules", "openclaw", "dist", "index.js");

        var gatewayScriptPath = Path.Combine(stateDir, OpenClawGatewayScriptFile);
        var watchdogScriptPath = Path.Combine(stateDir, OpenClawWatchdogScriptFile);

        await File.WriteAllTextAsync(
            gatewayScriptPath,
            BuildOpenClawGatewayScript(stateDir, configPath, home, openClawMainPath),
            ct);

        await File.WriteAllTextAsync(
            watchdogScriptPath,
            BuildOpenClawWatchdogScript(),
            ct);

        var (taskExitCode, taskOutput) = await RunSchtasksAsync(
            $"/create /tn \"{OpenClawTaskName}\" /sc onstart /ru SYSTEM /RL HIGHEST /TR \"{gatewayScriptPath}\" /F", ct);
        if (taskExitCode != 0)
            return new ServiceResult(false, $"Failed to register '{OpenClawTaskName}' task: {taskOutput}");

        var (watchdogExitCode, watchdogOutput) = await RunSchtasksAsync(
            $"/create /tn \"{OpenClawWatchdogTaskName}\" /sc minute /mo 1 /ru SYSTEM /RL HIGHEST /TR \"{watchdogScriptPath}\" /F", ct);
        if (watchdogExitCode != 0)
            return new ServiceResult(false, $"Failed to register '{OpenClawWatchdogTaskName}' task: {watchdogOutput}");

        return new ServiceResult(true, "OpenClaw gateway tasks configured.");
    }

    private static async Task<ServiceResult> CleanupOpenClawGatewayArtifactsAsync(CancellationToken ct)
    {
        var errors = new List<string>();

        await TryDeleteScheduledTaskAsync(OpenClawWatchdogTaskName, errors, ct);
        await TryDeleteScheduledTaskAsync(OpenClawTaskName, errors, ct);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            var stateDir = Path.Combine(home, OpenClawStateDirName);
            TryDeleteFile(Path.Combine(stateDir, OpenClawGatewayScriptFile), errors);
            TryDeleteFile(Path.Combine(stateDir, OpenClawWatchdogScriptFile), errors);
        }

        return errors.Count == 0
            ? new ServiceResult(true, "OpenClaw gateway artifacts removed.")
            : new ServiceResult(false, string.Join(Environment.NewLine, errors));
    }

    private static async Task TryDeleteScheduledTaskAsync(string taskName, List<string> errors, CancellationToken ct)
    {
        var (queryExitCode, queryOutput) = await RunSchtasksAsync($"/query /tn \"{taskName}\"", ct);
        if (queryExitCode != 0 && queryOutput.Contains("cannot find", StringComparison.OrdinalIgnoreCase))
            return;

        var (deleteExitCode, deleteOutput) = await RunSchtasksAsync($"/delete /tn \"{taskName}\" /F", ct);
        if (deleteExitCode != 0)
            errors.Add($"Failed to delete task '{taskName}': {deleteOutput}");
    }

    private static void TryDeleteFile(string path, List<string> errors)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to delete file '{path}': {ex.Message}");
        }
    }

    private static Task<(int ExitCode, string Output)> RunScAsync(string arguments, CancellationToken ct)
        => RunProcessAsync("sc.exe", arguments, ct);

    private static Task<(int ExitCode, string Output)> RunSchtasksAsync(string arguments, CancellationToken ct)
        => RunProcessAsync("schtasks.exe", arguments, ct);

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
