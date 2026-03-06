using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Chains multiple <see cref="IToolLoadoutRegistry"/> instances, checking each in
/// order from primary to fallback. Used to overlay user-defined (file-based) loadouts
/// on top of the built-in registry.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><c>GetLoadout</c> — first registry that has the name wins.</item>
///   <item><c>GetAll</c> — union of all registries, deduplicated by name (primary wins).</item>
///   <item><c>ResolveActivePlugins</c>/<c>ResolveDiscoverablePlugins</c> — merges all
///     loadouts from every registry so that cross-registry parent chains resolve
///     correctly (e.g. a YAML loadout inheriting from a built-in loadout).</item>
///   <item><c>Register</c> — forwards to the primary (first) registry.</item>
/// </list>
/// </remarks>
public sealed class CompositeToolLoadoutRegistry : IToolLoadoutRegistry
{
    private readonly IToolLoadoutRegistry[] _registries;

    /// <summary>Initialises the composite with the supplied ordered registries.</summary>
    /// <param name="registries">Ordered list — index 0 is checked first.</param>
    public CompositeToolLoadoutRegistry(params IToolLoadoutRegistry[] registries)
    {
        ArgumentNullException.ThrowIfNull(registries);
        if (registries.Length == 0)
            throw new ArgumentException("At least one registry must be supplied.", nameof(registries));

        _registries = registries;
    }

    /// <inheritdoc/>
    /// <remarks>Forwards to the primary (first) registry.</remarks>
    public void Register(ToolLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        _registries[0].Register(loadout);
    }

    /// <inheritdoc/>
    public ToolLoadout? GetLoadout(string name)
    {
        foreach (var registry in _registries)
        {
            var loadout = registry.GetLoadout(name);
            if (loadout is not null)
                return loadout;
        }

        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ToolLoadout> GetAll()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ToolLoadout>();

        foreach (var registry in _registries)
        {
            foreach (var loadout in registry.GetAll())
            {
                if (seen.Add(loadout.Name))
                    result.Add(loadout);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> ResolveActivePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins) =>
        LoadoutResolutionHelper.ResolveActivePlugins(
            loadoutName, availablePlugins, BuildMergedLoadouts());

    /// <inheritdoc/>
    public IReadOnlySet<string> ResolveDiscoverablePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins) =>
        LoadoutResolutionHelper.ResolveDiscoverablePlugins(
            loadoutName, availablePlugins, BuildMergedLoadouts());

    /// <summary>
    /// Merges all loadouts from every registry into a single dictionary, with
    /// earlier (primary) registries taking precedence on name conflicts.
    /// </summary>
    private Dictionary<string, ToolLoadout> BuildMergedLoadouts()
    {
        // Iterate in reverse so that primary registry values overwrite fallback values.
        var dict = new Dictionary<string, ToolLoadout>(StringComparer.OrdinalIgnoreCase);

        for (var i = _registries.Length - 1; i >= 0; i--)
        {
            foreach (var loadout in _registries[i].GetAll())
                dict[loadout.Name] = loadout;
        }

        return dict;
    }
}
