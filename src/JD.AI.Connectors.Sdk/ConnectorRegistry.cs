using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Connectors.Sdk;

/// <summary>
/// Discovers, registers, and manages <see cref="IConnector"/> implementations.
/// </summary>
public sealed class ConnectorRegistry
{
    private readonly Dictionary<string, ConnectorDescriptor> _connectors =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>All registered connectors.</summary>
    public IReadOnlyCollection<ConnectorDescriptor> All => _connectors.Values;

    /// <summary>
    /// Registers a connector and runs its <see cref="IConnector.Configure"/> method.
    /// </summary>
    /// <param name="connector">The connector instance.</param>
    /// <param name="services">The service collection to configure.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the connector is not decorated with <see cref="JdAiConnectorAttribute"/>.
    /// </exception>
    public ConnectorDescriptor Register(IConnector connector, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(services);

        var attr = connector.GetType().GetCustomAttributes(typeof(JdAiConnectorAttribute), inherit: false)
            .Cast<JdAiConnectorAttribute>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Connector type '{connector.GetType().Name}' must be decorated with [JdAiConnector].");

        var builder = new ConnectorBuilder(attr.Name, services);
        connector.Configure(builder);

        var descriptor = new ConnectorDescriptor
        {
            Name = attr.Name,
            DisplayName = attr.DisplayName,
            Version = attr.Version,
            Description = attr.Description,
            Connector = connector,
            ToolPluginTypes = builder.PluginTypes,
            Loadouts = builder.Loadouts,
        };

        _connectors[attr.Name] = descriptor;
        return descriptor;
    }

    /// <summary>
    /// Scans assemblies for types with <see cref="JdAiConnectorAttribute"/> and
    /// registers each found connector using a default (parameterless) constructor.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <param name="services">The service collection.</param>
    public void ScanAndRegister(IEnumerable<System.Reflection.Assembly> assemblies, IServiceCollection services)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (!type.IsClass || type.IsAbstract) continue;
                if (!typeof(IConnector).IsAssignableFrom(type)) continue;

                var attr = type.GetCustomAttributes(typeof(JdAiConnectorAttribute), inherit: false)
                    .OfType<JdAiConnectorAttribute>()
                    .FirstOrDefault();
                if (attr is null) continue;

                var instance = (IConnector)Activator.CreateInstance(type)!;
                Register(instance, services);
            }
        }
    }

    /// <summary>
    /// Gets a registered connector by name.
    /// </summary>
    public ConnectorDescriptor? Get(string name) =>
        _connectors.GetValueOrDefault(name);

    /// <summary>Enables or disables a registered connector by name.</summary>
    /// <returns><c>true</c> if the connector was found and updated.</returns>
    public bool SetEnabled(string name, bool enabled)
    {
        if (!_connectors.TryGetValue(name, out var descriptor)) return false;
        descriptor.IsEnabled = enabled;
        return true;
    }
}

/// <summary>Internal implementation of <see cref="IConnectorBuilder"/>.</summary>
internal sealed class ConnectorBuilder : IConnectorBuilder
{
    private readonly List<Type> _pluginTypes = [];
    private readonly Dictionary<string, Func<string, bool>> _loadouts =
        new(StringComparer.OrdinalIgnoreCase);

    public ConnectorBuilder(string connectorName, IServiceCollection services)
    {
        ConnectorName = connectorName;
        Services = services;
    }

    public IServiceCollection Services { get; }
    public string ConnectorName { get; }

    public IReadOnlyList<Type> PluginTypes => _pluginTypes;
    public IReadOnlyDictionary<string, Func<string, bool>> Loadouts => _loadouts;

    public IConnectorBuilder AddAuthentication<TAuth>() where TAuth : class, IConnectorAuthProvider
    {
        Services.AddScoped<IConnectorAuthProvider, TAuth>();
        Services.AddScoped<TAuth>();
        return this;
    }

    public IConnectorBuilder AddToolPlugin<TPlugin>() where TPlugin : class
    {
        _pluginTypes.Add(typeof(TPlugin));
        Services.AddTransient<TPlugin>();
        return this;
    }

    public IConnectorBuilder AddLoadout(string loadoutName, Func<string, bool> filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loadoutName);
        ArgumentNullException.ThrowIfNull(filter);
        _loadouts[loadoutName] = filter;
        return this;
    }
}
