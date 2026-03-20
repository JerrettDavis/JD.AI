using JD.AI.Channels.OpenClaw;
using JD.AI.Core.Infrastructure;
using JD.AI.Gateway.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Handles `jdai-daemon bridge` command actions and runtime cleanup orchestration.
/// </summary>
public class BridgeCommandService
{
    private readonly string _appSettingsPath;
    private readonly Func<IServiceManager> _serviceManagerFactory;

    public BridgeCommandService(
        string appSettingsPath,
        Func<IServiceManager> serviceManagerFactory)
    {
        _appSettingsPath = appSettingsPath;
        _serviceManagerFactory = serviceManagerFactory;
    }

    public async Task<int> ExecuteAsync(string? action)
    {
        var normalized = (action ?? "status").Trim().ToLowerInvariant();
        try
        {
            OpenClawBridgeState state;
            switch (normalized)
            {
                case "status":
                    state = ReadState(_appSettingsPath);
                    WriteStatus(state, _appSettingsPath);
                    return 0;
                case "enable":
                    state = SetEnabled(_appSettingsPath, enabled: true);
                    await SetOpenClawGatewayTasksEnabledAsync(enabled: true).ConfigureAwait(false);
                    await RestartInstalledServiceAsync().ConfigureAwait(false);
                    WriteStatus(state, _appSettingsPath);
                    return 0;
                case "disable":
                    await DisableBridgeRuntimeAsync(_appSettingsPath).ConfigureAwait(false);
                    state = SetEnabled(_appSettingsPath, enabled: false);
                    await SetOpenClawGatewayTasksEnabledAsync(enabled: false).ConfigureAwait(false);
                    await StopOpenClawGatewayTaskAsync().ConfigureAwait(false);
                    await RestartInstalledServiceAsync().ConfigureAwait(false);
                    WriteStatus(state, _appSettingsPath);
                    return 0;
                case "passthrough":
                    state = SetPassthrough(_appSettingsPath);
                    await SetOpenClawGatewayTasksEnabledAsync(enabled: true).ConfigureAwait(false);
                    await RestartInstalledServiceAsync().ConfigureAwait(false);
                    WriteStatus(state, _appSettingsPath);
                    return 0;
                default:
                    Console.Error.WriteLine("Usage: jdai-daemon bridge [status|enable|disable|passthrough]");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Bridge command failed: {ex.Message}");
            return 1;
        }
    }

    protected virtual OpenClawBridgeState ReadState(string appSettingsPath) =>
        OpenClawBridgeConfigEditor.ReadState(appSettingsPath);

    protected virtual OpenClawBridgeState SetEnabled(string appSettingsPath, bool enabled) =>
        OpenClawBridgeConfigEditor.SetEnabled(appSettingsPath, enabled);

    protected virtual OpenClawBridgeState SetPassthrough(string appSettingsPath) =>
        OpenClawBridgeConfigEditor.SetPassthrough(appSettingsPath);

    protected virtual void WriteStatus(OpenClawBridgeState state, string appSettingsPath)
    {
        var mode = state.Enabled
            ? state.OverrideActive ? "Override active" : "Passthrough/observe-only"
            : "Disabled";

        Console.WriteLine($"Config:          {appSettingsPath}");
        Console.WriteLine($"Bridge enabled:  {state.Enabled}");
        Console.WriteLine($"Auto-connect:    {state.AutoConnect}");
        Console.WriteLine($"Default mode:    {state.DefaultMode}");
        Console.WriteLine($"Override active: {state.OverrideActive}");
        Console.WriteLine($"Effective mode:  {mode}");
        if (state.OverrideChannels.Count > 0)
            Console.WriteLine($"Override chans:  {string.Join(", ", state.OverrideChannels)}");
    }

    protected virtual async Task RestartInstalledServiceAsync()
    {
        try
        {
            var manager = _serviceManagerFactory();
            var status = await manager.GetStatusAsync().ConfigureAwait(false);
            if (status.State != ServiceState.Running)
                return;

            var stop = await manager.StopAsync().ConfigureAwait(false);
            if (!stop.Success)
            {
                Console.WriteLine("Service restart note: could not stop running service automatically.");
                return;
            }

            var start = await manager.StartAsync().ConfigureAwait(false);
            if (!start.Success)
                Console.WriteLine("Service restart note: service stopped but could not be restarted automatically.");
            else
                Console.WriteLine("Service restart note: running service was restarted to apply bridge changes.");
        }
        catch (PlatformNotSupportedException)
        {
            // Service-manager restart is only available on supported OSes.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service restart note: automatic restart failed ({ex.Message}).");
        }
    }

    protected virtual async Task DisableBridgeRuntimeAsync(string appSettingsPath)
    {
        var runtimeCleanupSucceeded = false;
        var config = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: true, reloadOnChange: false)
            .Build();

        try
        {
            var port = config.GetValue<int?>("Gateway:Server:Port") ?? GatewayRuntimeDefaults.DefaultPort;
            var disableUrl = new Uri($"http://127.0.0.1:{port}/api/gateway/openclaw/bridge/disable");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var response = await client.PostAsync(disableUrl, null).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Runtime bridge cleanup note: active OpenClaw sessions were cleaned.");
                runtimeCleanupSucceeded = true;
            }
            else
                Console.WriteLine($"Runtime bridge cleanup note: HTTP {(int)response.StatusCode}; continuing with config disable.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Runtime bridge cleanup note: skipped ({ex.Message}).");
        }

        if (runtimeCleanupSucceeded)
            return;

        await DisableBridgeRuntimeDirectAsync(config).ConfigureAwait(false);
    }

    protected virtual async Task DisableBridgeRuntimeDirectAsync(IConfiguration config)
    {
        try
        {
            var openClawGatewayConfig = config.GetSection("Gateway:OpenClaw").Get<OpenClawGatewayConfig>()
                                      ?? new OpenClawGatewayConfig();
            var openClawConfig = new OpenClawConfig
            {
                WebSocketUrl = openClawGatewayConfig.WebSocketUrl,
                OpenClawStateDir = config["Gateway:OpenClaw:StateDir"],
            };

            OpenClawIdentityLoader.LoadDeviceIdentity(openClawConfig, openClawConfig.OpenClawStateDir);
            if (!OpenClawIdentityLoader.HasRequiredIdentity(openClawConfig))
            {
                Console.WriteLine("Runtime bridge cleanup note: direct cleanup skipped (OpenClaw identity is incomplete).");
                return;
            }

            await using var rpc = new OpenClawRpcClient(
                openClawConfig,
                NullLogger<OpenClawRpcClient>.Instance);
            await using var bridge = new OpenClawBridgeChannel(
                rpc,
                NullLogger<OpenClawBridgeChannel>.Instance,
                openClawConfig);

            await bridge.ConnectAsync().ConfigureAwait(false);
            var (prefixes, contains) = BuildManagedSessionFilters(openClawGatewayConfig);
            var deleted = await bridge.DeleteSessionsByPrefixAsync(
                prefixes,
                contains,
                deleteTranscript: true).ConfigureAwait(false);
            await bridge.DisconnectAsync().ConfigureAwait(false);

            Console.WriteLine($"Runtime bridge cleanup note: direct OpenClaw cleanup removed {deleted} managed session(s).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Runtime bridge cleanup note: direct cleanup failed ({ex.Message}).");
        }
    }

    internal static (string[] Prefixes, string[] Contains) BuildManagedSessionFilters(OpenClawGatewayConfig config)
    {
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "agent:jdai-"
        };
        var contains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "g-agent-"
        };

        foreach (var registration in config.RegisterAgents)
        {
            if (!string.IsNullOrWhiteSpace(registration.Id))
            {
                var agentId = registration.Id.Trim();
                prefixes.Add($"agent:{agentId}:");
                contains.Add(agentId);
            }

            foreach (var binding in registration.Bindings)
            {
                if (!string.IsNullOrWhiteSpace(binding.Channel))
                    contains.Add($"{binding.Channel.Trim()}:g-agent-");
            }
        }

        foreach (var channel in config.Channels.Keys)
        {
            if (!string.IsNullOrWhiteSpace(channel))
                contains.Add($"{channel.Trim()}:g-agent-");
        }

        return (prefixes.ToArray(), contains.ToArray());
    }

    protected virtual async Task SetOpenClawGatewayTasksEnabledAsync(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var mode = enabled ? "/enable" : "/disable";
        foreach (var taskName in new[] { "\\OpenClaw Gateway Watchdog", "\\OpenClaw Gateway" })
        {
            var result = await ProcessExecutor.RunAsync(
                "schtasks.exe",
                $"/change /tn \"{taskName}\" {mode}",
                timeout: TimeSpan.FromSeconds(8)).ConfigureAwait(false);

            if (!result.Success)
                Console.WriteLine($"Bridge task note: could not {(enabled ? "enable" : "disable")} {taskName} ({result.StandardError})");
        }
    }

    protected virtual async Task StopOpenClawGatewayTaskAsync()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var result = await ProcessExecutor.RunAsync(
            "schtasks.exe",
            "/end /tn \"\\OpenClaw Gateway\"",
            timeout: TimeSpan.FromSeconds(8)).ConfigureAwait(false);
        if (!result.Success)
            Console.WriteLine($"Bridge task note: could not end OpenClaw Gateway task ({result.StandardError})");
    }
}
