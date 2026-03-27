using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Gateway.Client;

/// <summary>
/// Extension methods for registering JD.AI Gateway client services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Gateway HTTP client and SignalR client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="gatewayUrl">Base URL of the Gateway (e.g., "http://localhost:5100").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGatewayClient(this IServiceCollection services, string gatewayUrl)
    {
        var baseUrl = gatewayUrl.TrimEnd('/');

        services.AddHttpClient<GatewayHttpClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton(new GatewaySignalRClient(baseUrl));

        return services;
    }
}
