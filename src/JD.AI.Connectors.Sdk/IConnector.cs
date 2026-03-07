using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Connectors.Sdk;

/// <summary>
/// A JD.AI connector that registers tool plugins, auth providers, and loadouts
/// into the dependency injection container.
/// </summary>
public interface IConnector
{
    /// <summary>
    /// Called during startup to register the connector's tools, auth providers, and loadouts.
    /// </summary>
    /// <param name="builder">The connector builder targeting this connector's DI scope.</param>
    void Configure(IConnectorBuilder builder);
}

/// <summary>
/// Fluent builder for registering connector components.
/// </summary>
public interface IConnectorBuilder
{
    /// <summary>The underlying service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>The connector name from its <see cref="JdAiConnectorAttribute"/>.</summary>
    string ConnectorName { get; }

    /// <summary>
    /// Registers an authentication provider for this connector.
    /// </summary>
    /// <typeparam name="TAuth">The auth provider type implementing <see cref="IConnectorAuthProvider"/>.</typeparam>
    IConnectorBuilder AddAuthentication<TAuth>() where TAuth : class, IConnectorAuthProvider;

    /// <summary>
    /// Registers a Semantic Kernel plugin type as a connector tool plugin.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin class decorated with Semantic Kernel tool attributes.</typeparam>
    IConnectorBuilder AddToolPlugin<TPlugin>() where TPlugin : class;

    /// <summary>
    /// Registers a named tool loadout scoped to this connector.
    /// </summary>
    /// <param name="loadoutName">Name of the loadout (e.g. "jira-readonly").</param>
    /// <param name="filter">Predicate to select which tool names belong to this loadout.</param>
    IConnectorBuilder AddLoadout(string loadoutName, Func<string, bool> filter);
}
