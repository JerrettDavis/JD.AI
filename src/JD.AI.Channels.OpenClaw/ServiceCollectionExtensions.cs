using System.Text.Json;
using JD.AI.Channels.OpenClaw.Routing;
using JD.AI.Core.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// DI registration helpers for the OpenClaw bridge channel.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OpenClawBridgeChannel"/> as an <see cref="IChannel"/>
    /// with WebSocket JSON-RPC connectivity to an OpenClaw gateway.
    /// </summary>
    public static IServiceCollection AddOpenClawBridge(
        this IServiceCollection services,
        Action<OpenClawConfig> configure)
    {
        var config = new OpenClawConfig();
        configure(config);

        // Auto-load device identity from OpenClaw state directory if not explicitly set
        if (string.IsNullOrEmpty(config.DeviceId))
        {
            var stateDir = config.OpenClawStateDir
                ?? ResolveOpenClawStateDir();

            LoadDeviceIdentity(config, stateDir);
        }

        services.AddSingleton(config);
        services.AddSingleton<OpenClawRpcClient>();
        services.AddSingleton<OpenClawBridgeChannel>();
        services.AddSingleton<IChannel>(sp => sp.GetRequiredService<OpenClawBridgeChannel>());

        return services;
    }

    /// <summary>
    /// Registers the OpenClaw routing infrastructure with per-channel mode configuration.
    /// Call after <see cref="AddOpenClawBridge"/> to enable intelligent message routing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configure routing options (modes, channels, agent profiles).</param>
    /// <param name="messageProcessor">
    /// Callback that processes a user message through JD.AI's agent.
    /// Parameters: (sessionKey, content) → response string.
    /// </param>
    public static IServiceCollection AddOpenClawRouting(
        this IServiceCollection services,
        Action<OpenClawRoutingConfig> configure,
        Func<string, string, Task<string?>>? messageProcessor = null)
    {
        services.Configure(configure);

        // Register mode handlers
        services.AddSingleton<IOpenClawModeHandler, PassthroughModeHandler>();
        services.AddSingleton<IOpenClawModeHandler, InterceptModeHandler>();
        services.AddSingleton<IOpenClawModeHandler, ProxyModeHandler>();
        services.AddSingleton<IOpenClawModeHandler, SidecarModeHandler>();

        // Register the message processor callback
        if (messageProcessor is not null)
        {
            services.AddSingleton(messageProcessor);
        }
        else
        {
            // Default no-op processor — consumers should replace this
            services.AddSingleton<Func<string, string, Task<string?>>>(
                (_, _) => Task.FromResult<string?>(null));
        }

        // Register the routing service
        services.AddHostedService<OpenClawRoutingService>();

        return services;
    }

    /// <summary>
    /// Loads device identity and auth token from the OpenClaw state directory.
    /// Reads ~/.openclaw/identity/device.json and device-auth.json.
    /// </summary>
    private static void LoadDeviceIdentity(OpenClawConfig config, string stateDir)
    {
        var devicePath = Path.Combine(stateDir, "identity", "device.json");
        var authPath = Path.Combine(stateDir, "identity", "device-auth.json");

        if (!File.Exists(devicePath) || !File.Exists(authPath))
            return;

        using var deviceDoc = JsonDocument.Parse(File.ReadAllText(devicePath));
        var deviceRoot = deviceDoc.RootElement;

        config.DeviceId = deviceRoot.GetProperty("deviceId").GetString() ?? "";
        config.PublicKeyPem = deviceRoot.GetProperty("publicKeyPem").GetString() ?? "";
        config.PrivateKeyPem = deviceRoot.GetProperty("privateKeyPem").GetString() ?? "";

        using var authDoc = JsonDocument.Parse(File.ReadAllText(authPath));
        var authRoot = authDoc.RootElement;

        if (authRoot.TryGetProperty("tokens", out var tokens)
            && tokens.TryGetProperty("operator", out var op)
            && op.TryGetProperty("token", out var token))
        {
            config.DeviceToken = token.GetString() ?? "";
        }

        // Load the gateway shared token from openclaw.json
        if (string.IsNullOrEmpty(config.GatewayToken))
        {
            var configPath = Path.Combine(stateDir, "openclaw.json");
            if (File.Exists(configPath))
            {
                using var configDoc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (configDoc.RootElement.TryGetProperty("gateway", out var gw)
                    && gw.TryGetProperty("auth", out var gwAuth)
                    && gwAuth.TryGetProperty("token", out var gwToken))
                {
                    config.GatewayToken = gwToken.GetString() ?? "";
                }
            }
        }
    }

    /// <summary>
    /// Resolves the OpenClaw state directory, scanning user profiles when
    /// running as a service account (LocalSystem, root, etc.).
    /// </summary>
    private static string ResolveOpenClawStateDir()
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userHome))
        {
            var candidate = Path.Combine(userHome, ".openclaw");
            if (Directory.Exists(candidate))
                return candidate;
        }

        // Scan user profiles (same logic as DataDirectories)
        string? profilesRoot = null;
        if (OperatingSystem.IsWindows())
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            profilesRoot = Path.Combine(systemDrive, "Users");
        }
        else if (OperatingSystem.IsLinux())
            profilesRoot = "/home";
        else if (OperatingSystem.IsMacOS())
            profilesRoot = "/Users";

        if (profilesRoot is not null && Directory.Exists(profilesRoot))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(profilesRoot))
                {
                    var candidate = Path.Combine(dir, ".openclaw");
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        // Fallback to current user's home
        return Path.Combine(userHome ?? ".", ".openclaw");
    }
}
