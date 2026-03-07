using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Shared resolution logic for loadout registries. Encapsulates inheritance-chain
/// traversal, active-plugin resolution, and discoverable-plugin resolution so that
/// multiple <see cref="IToolLoadoutRegistry"/> implementations can share the behaviour
/// without duplicating code.
/// </summary>
internal static class LoadoutResolutionHelper
{
    internal static IReadOnlySet<string> ResolveActivePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins,
        IReadOnlyDictionary<string, ToolLoadout> loadouts)
    {
        var plugins = availablePlugins.ToList();
        var (defaultPlugins, categories, disabled) = ResolveInheritedSettings(loadoutName, loadouts);

        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in plugins)
        {
            if (disabled.Contains(plugin.Name))
                continue;

            if (defaultPlugins.Contains(plugin.Name))
            {
                active.Add(plugin.Name);
                continue;
            }

            if (ToolLoadoutRegistry.PluginCategoryMap.TryGetValue(plugin.Name, out var cat) &&
                categories.Contains(cat))
                active.Add(plugin.Name);
        }

        return active;
    }

    internal static IReadOnlySet<string> ResolveDiscoverablePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins,
        IReadOnlyDictionary<string, ToolLoadout> loadouts)
    {
        var plugins = availablePlugins.ToList();
        var active = ResolveActivePlugins(loadoutName, plugins, loadouts);
        var (_, _, disabled) = ResolveInheritedSettings(loadoutName, loadouts);
        var patterns = ResolveDiscoverablePatterns(loadoutName, loadouts);

        var discoverable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in plugins)
        {
            if (active.Contains(plugin.Name) || disabled.Contains(plugin.Name))
                continue;

            foreach (var pattern in patterns)
            {
                if (ToolLoadoutRegistry.MatchesPattern(plugin.Name, pattern))
                {
                    discoverable.Add(plugin.Name);
                    break;
                }
            }
        }

        return discoverable;
    }

    private static (HashSet<string> DefaultPlugins, HashSet<ToolCategory> Categories, HashSet<string> Disabled)
        ResolveInheritedSettings(string loadoutName, IReadOnlyDictionary<string, ToolLoadout> loadouts)
    {
        var chain = BuildInheritanceChain(loadoutName, loadouts);
        var defaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categories = new HashSet<ToolCategory>();
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var loadout in chain)
        {
            foreach (var p in loadout.DefaultPlugins) defaultPlugins.Add(p);
            foreach (var c in loadout.IncludedCategories) categories.Add(c);
            foreach (var d in loadout.DisabledPlugins) disabled.Add(d);
        }

        return (defaultPlugins, categories, disabled);
    }

    private static List<string> ResolveDiscoverablePatterns(
        string loadoutName, IReadOnlyDictionary<string, ToolLoadout> loadouts)
    {
        var chain = BuildInheritanceChain(loadoutName, loadouts);
        var patterns = new List<string>();
        foreach (var loadout in chain)
            patterns.AddRange(loadout.DiscoverablePatterns);
        return patterns;
    }

    internal static List<ToolLoadout> BuildInheritanceChain(
        string loadoutName, IReadOnlyDictionary<string, ToolLoadout> loadouts)
    {
        var chain = new List<ToolLoadout>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = loadoutName;

        while (current is not null)
        {
            if (!visited.Add(current))
                break; // cycle guard

            if (!loadouts.TryGetValue(current, out var loadout))
                break; // unknown loadout — stop

            chain.Add(loadout);
            current = loadout.ParentLoadoutName;
        }

        chain.Reverse(); // root-first application order
        return chain;
    }
}
