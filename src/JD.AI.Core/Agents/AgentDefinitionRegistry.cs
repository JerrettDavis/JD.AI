using System.Collections.Concurrent;

namespace JD.AI.Core.Agents;

/// <summary>
/// Thread-safe in-memory <see cref="IAgentDefinitionRegistry"/>.
/// </summary>
public sealed class AgentDefinitionRegistry : IAgentDefinitionRegistry
{
    private readonly ConcurrentDictionary<string, AgentDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> GetAll() =>
        [.. _definitions.Values];

    /// <inheritdoc />
    public AgentDefinition? GetByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions.TryGetValue(name, out var def) ? def : null;
    }

    /// <inheritdoc />
    public void Register(AgentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("AgentDefinition.Name must not be empty.", nameof(definition));

        _definitions[definition.Name] = definition;
    }

    public bool Unregister(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _definitions.TryRemove(name, out _);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentDefinition> GetByTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return [.. _definitions.Values.Where(d => d.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))];
    }
}
