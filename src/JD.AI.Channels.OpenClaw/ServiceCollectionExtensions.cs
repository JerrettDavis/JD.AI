using JD.AI.Core.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// DI registration helpers for the OpenClaw bridge channel.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OpenClawBridgeChannel"/> as an <see cref="IChannel"/> and
    /// configures an <see cref="HttpClient"/> for communicating with OpenClaw.
    /// </summary>
    public static IServiceCollection AddOpenClawBridge(
        this IServiceCollection services,
        Action<OpenClawConfig> configure)
    {
        var config = new OpenClawConfig();
        configure(config);

        services.AddSingleton(config);
        services.AddHttpClient<OpenClawBridgeChannel>(client =>
        {
            client.BaseAddress = new Uri(config.BaseUrl);
            if (!string.IsNullOrEmpty(config.ApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", config.ApiKey);
        });
        services.AddSingleton<IChannel>(sp => sp.GetRequiredService<OpenClawBridgeChannel>());

        return services;
    }
}
