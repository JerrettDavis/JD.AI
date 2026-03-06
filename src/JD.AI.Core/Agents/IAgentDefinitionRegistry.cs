namespace JD.AI.Core.Agents;

/// <summary>
/// Registry for <see cref="AgentDefinition"/> instances loaded from
/// YAML files or registered programmatically.
/// </summary>
public interface IAgentDefinitionRegistry
{
    /// <summary>Returns all registered agent definitions.</summary>
    IReadOnlyList<AgentDefinition> GetAll();

    /// <summary>
    /// Returns the definition with the given <paramref name="name"/>,
    /// or <c>null</c> if no matching definition is registered.
    /// </summary>
    AgentDefinition? GetByName(string name);

    /// <summary>
    /// Registers a definition. If a definition with the same
    /// <paramref name="definition.Name"/> already exists it is replaced.
    /// </summary>
    void Register(AgentDefinition definition);

    /// <summary>Returns all definitions that carry the specified <paramref name="tag"/>.</summary>
    IReadOnlyList<AgentDefinition> GetByTag(string tag);
}
