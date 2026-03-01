using System.Text.Json;
using JD.AI.Core.Channels;
using Microsoft.Extensions.DependencyInjection;

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
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw");

            LoadDeviceIdentity(config, stateDir);
        }

        services.AddSingleton(config);
        services.AddSingleton<OpenClawRpcClient>();
        services.AddSingleton<OpenClawBridgeChannel>();
        services.AddSingleton<IChannel>(sp => sp.GetRequiredService<OpenClawBridgeChannel>());

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
}
