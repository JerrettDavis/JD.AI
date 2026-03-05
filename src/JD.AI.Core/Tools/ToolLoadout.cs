namespace JD.AI.Core.Tools;

/// <summary>
/// Represents a named, curated set of tools to expose to an agent.
/// Defines which plugins are loaded by default, which are discoverable on demand,
/// and which are explicitly disabled.
/// </summary>
/// <remarks>
/// Use <see cref="ToolLoadoutBuilder"/> to construct instances via a fluent API.
/// Well-known built-in names are defined in <see cref="WellKnownLoadouts"/>.
/// </remarks>
public sealed class ToolLoadout
{
    /// <summary>Unique name identifying this loadout.</summary>
    public string Name { get; }

    /// <summary>
    /// Optional name of the parent loadout whose settings are inherited before applying
    /// this loadout's own additions. Cycles are silently broken.
    /// </summary>
    public string? ParentLoadoutName { get; init; }

    /// <summary>
    /// Explicit plugin names included by default regardless of category.
    /// Matching is case-insensitive.
    /// </summary>
    public IReadOnlySet<string> DefaultPlugins { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tool categories whose associated plugins are loaded by default.
    /// The registry maps each <see cref="ToolCategory"/> to a set of plugin names.
    /// </summary>
    public IReadOnlySet<ToolCategory> IncludedCategories { get; init; } =
        new HashSet<ToolCategory>();

    /// <summary>
    /// Glob-style patterns for plugins that are discoverable but not eagerly loaded.
    /// Supports exact names and wildcard suffixes (e.g. <c>"docker*"</c>, <c>"github"</c>).
    /// A single <c>"*"</c> makes all unloaded plugins discoverable.
    /// </summary>
    public IReadOnlyList<string> DiscoverablePatterns { get; init; } = [];

    /// <summary>
    /// Plugin names that are explicitly blocked, overriding any category or default inclusion.
    /// Matching is case-insensitive.
    /// </summary>
    public IReadOnlySet<string> DisabledPlugins { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initialises a loadout with the given unique name.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public ToolLoadout(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
