using JD.AI.Channels.OpenClaw.Routing;
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
            OpenClawIdentityLoader.LoadDeviceIdentity(config, config.OpenClawStateDir);
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
}
