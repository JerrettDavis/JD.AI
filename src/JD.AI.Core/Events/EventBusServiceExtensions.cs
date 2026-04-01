using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace JD.AI.Core.Events;

/// <summary>
///     Configuration options for the event bus infrastructure.
/// </summary>
public sealed class EventBusOptions
{
    /// <summary>
    ///     The event bus provider: <c>"InProcess"</c> (default) or <c>"Redis"</c>.
    /// </summary>
    public string Provider { get; set; } = "InProcess";

    /// <summary>
    ///     Redis connection string. Required when <see cref="Provider" /> is <c>"Redis"</c>.
    /// </summary>
    public string? RedisConnectionString { get; set; }
}

/// <summary>
///     Extension methods for registering event bus services.
/// </summary>
public static class EventBusServiceExtensions
{
    /// <summary>
    ///     Adds the event bus to the service collection. Uses <see cref="InProcessEventBus" />
    ///     by default; set <c>EventBus:Provider</c> to <c>"Redis"</c> and provide
    ///     <c>EventBus:RedisConnectionString</c> to switch to distributed mode.
    /// </summary>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        EventBusOptions? options = null)
    {
        options ??= new EventBusOptions();

        if (string.Equals(options.Provider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
                throw new InvalidOperationException(
                    "EventBus:RedisConnectionString is required when Provider is 'Redis'.");

            var connectionString = options.RedisConnectionString;
            services.AddSingleton<IEventBus>(sp =>
            {
                try
                {
                    var configOpts = ConfigurationOptions.Parse(connectionString);
                    configOpts.ConnectTimeout = 3000;
                    configOpts.AbortOnConnectFail = true;
                    var mux = ConnectionMultiplexer.Connect(configOpts);
                    var logger = sp.GetRequiredService<ILogger<RedisEventBus>>();
                    return new RedisEventBus(mux, logger);
                }
                catch
                {
                    // Redis unavailable — fall back to in-process event bus
                    return InMemoryEventBus.Create();
                }
            });
        }
        else
            services.AddSingleton<IEventBus>(InMemoryEventBus.Create());

        return services;
    }
}
