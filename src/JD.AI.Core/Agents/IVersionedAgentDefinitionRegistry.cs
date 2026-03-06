namespace JD.AI.Core.Agents;

/// <summary>
/// Versioned registry for <see cref="AgentDefinition"/> instances supporting
/// environment-scoped storage, semantic-version resolution, and environment promotion.
/// </summary>
public interface IVersionedAgentDefinitionRegistry : IAgentDefinitionRegistry
{
    /// <summary>
    /// Persists the definition in the given environment scope.
    /// If a definition with the same name+version already exists it is overwritten.
    /// </summary>
    Task RegisterAsync(
        AgentDefinition definition,
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a definition by name and optional version within the given environment.
    /// If <paramref name="version"/> is null or <c>"latest"</c>, the highest semver
    /// definition is returned.  Returns <c>null</c> if not found.
    /// </summary>
    Task<AgentDefinition?> ResolveAsync(
        string name,
        string? version = null,
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all definitions in the given environment, optionally filtered by name.
    /// </summary>
    Task<IReadOnlyList<AgentDefinition>> ListAsync(
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the named version from the given environment.</summary>
    Task UnregisterAsync(
        string name,
        string version,
        string environment = AgentEnvironments.Dev,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a definition from <paramref name="fromEnvironment"/> to
    /// <paramref name="toEnvironment"/>. The definition must be registered in the
    /// source environment.
    /// </summary>
    Task PromoteAsync(
        string name,
        string version,
        string fromEnvironment,
        string toEnvironment,
        CancellationToken cancellationToken = default);
}
