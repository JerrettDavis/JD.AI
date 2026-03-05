namespace JD.AI.Core.Tools;

/// <summary>
/// Fluent builder for creating <see cref="ToolLoadout"/> instances.
/// </summary>
/// <example>
/// <code>
/// var loadout = ToolLoadoutBuilder
///     .Create("myloadout")
///     .Extends("minimal")
///     .IncludeCategory(ToolCategory.Git)
///     .AddDiscoverable("docker*")
///     .Build();
/// </code>
/// </example>
public sealed class ToolLoadoutBuilder
{
    private readonly string _name;
    private string? _parent;
    private readonly HashSet<string> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ToolCategory> _categories = [];
    private readonly List<string> _discoverable = [];
    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);

    private ToolLoadoutBuilder(string name) => _name = name;

    /// <summary>Creates a new builder for the given loadout name.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static ToolLoadoutBuilder Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ToolLoadoutBuilder(name);
    }

    /// <summary>Sets the parent loadout name from which settings are inherited.</summary>
    public ToolLoadoutBuilder Extends(string parentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentName);
        _parent = parentName;
        return this;
    }

    /// <summary>Adds a specific plugin by name to the set of default-loaded plugins.</summary>
    public ToolLoadoutBuilder AddPlugin(string pluginName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);
        _plugins.Add(pluginName);
        return this;
    }

    /// <summary>Includes all plugins belonging to the given category in the default set.</summary>
    public ToolLoadoutBuilder IncludeCategory(ToolCategory category)
    {
        _categories.Add(category);
        return this;
    }

    /// <summary>
    /// Marks a plugin pattern as discoverable (lazy) — available on request but not
    /// eagerly loaded. Supports exact names and wildcard suffixes (e.g. <c>"docker*"</c>).
    /// </summary>
    public ToolLoadoutBuilder AddDiscoverable(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        _discoverable.Add(pattern);
        return this;
    }

    /// <summary>
    /// Explicitly disables a plugin by name, overriding any category or default inclusion.
    /// </summary>
    public ToolLoadoutBuilder Disable(string pluginName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);
        _disabled.Add(pluginName);
        return this;
    }

    /// <summary>Builds and returns the immutable <see cref="ToolLoadout"/>.</summary>
    public ToolLoadout Build() => new(_name)
    {
        ParentLoadoutName = _parent,
        DefaultPlugins = _plugins,
        IncludedCategories = _categories,
        DiscoverablePatterns = _discoverable.AsReadOnly(),
        DisabledPlugins = _disabled,
    };
}
