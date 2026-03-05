using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Default implementation of <see cref="IToolLoadoutRegistry"/>.
/// Pre-populated with five built-in loadouts:
/// <list type="bullet">
///   <item><see cref="ToolLoadout.Names.Minimal"/></item>
///   <item><see cref="ToolLoadout.Names.Developer"/></item>
///   <item><see cref="ToolLoadout.Names.DevOps"/></item>
///   <item><see cref="ToolLoadout.Names.Research"/></item>
///   <item><see cref="ToolLoadout.Names.Full"/></item>
/// </list>
/// </summary>
public sealed class ToolLoadoutRegistry : IToolLoadoutRegistry
{
    /// <summary>
    /// Maps plugin names (as registered via <c>AddFromType</c> / <c>AddFromObject</c>)
    /// to their logical <see cref="ToolCategory"/>. Plugin names are the strings passed
    /// as the second argument to those calls and are matched case-insensitively.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ToolCategory> PluginCategoryMap =
        new Dictionary<string, ToolCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Filesystem ──────────────────────────────────────────────────
            ["file"] = ToolCategory.Filesystem,
            ["batchEdit"] = ToolCategory.Filesystem,
            ["diff"] = ToolCategory.Filesystem,
            ["notebook"] = ToolCategory.Filesystem,
            ["migration"] = ToolCategory.Filesystem,

            // ── Git ─────────────────────────────────────────────────────────
            ["git"] = ToolCategory.Git,

            // ── GitHub ──────────────────────────────────────────────────────
            ["github"] = ToolCategory.GitHub,

            // ── Shell ───────────────────────────────────────────────────────
            ["shell"] = ToolCategory.Shell,
            ["environment"] = ToolCategory.Shell,
            ["clipboard"] = ToolCategory.Shell,
            ["runtime"] = ToolCategory.Shell,

            // ── Web ─────────────────────────────────────────────────────────
            ["web"] = ToolCategory.Web,
            ["browser"] = ToolCategory.Web,

            // ── Search ──────────────────────────────────────────────────────
            ["search"] = ToolCategory.Search,
            ["websearch"] = ToolCategory.Search,

            // ── Network ─────────────────────────────────────────────────────
            ["tailscale"] = ToolCategory.Network,

            // ── Memory ──────────────────────────────────────────────────────
            ["memory"] = ToolCategory.Memory,

            // ── Orchestration ────────────────────────────────────────────────
            ["tasks"] = ToolCategory.Orchestration,
            ["sessions"] = ToolCategory.Orchestration,
            ["mcp"] = ToolCategory.Orchestration,
            ["mcpEcosystem"] = ToolCategory.Orchestration,
            ["capabilities"] = ToolCategory.Orchestration,
            ["channels"] = ToolCategory.Orchestration,
            ["gateway"] = ToolCategory.Orchestration,
            ["subagent"] = ToolCategory.Orchestration,

            // ── Analysis ─────────────────────────────────────────────────────
            ["think"] = ToolCategory.Analysis,
            ["parityDocs"] = ToolCategory.Analysis,
            ["skillParity"] = ToolCategory.Analysis,
            ["benchmark"] = ToolCategory.Analysis,
            ["usage"] = ToolCategory.Analysis,

            // ── Scheduling ───────────────────────────────────────────────────
            ["scheduler"] = ToolCategory.Scheduling,

            // ── Multimodal ────────────────────────────────────────────────────
            ["multimodal"] = ToolCategory.Multimodal,

            // ── Security ──────────────────────────────────────────────────────
            ["policy"] = ToolCategory.Security,
            ["encoding"] = ToolCategory.Security,
        };

    private readonly Dictionary<string, ToolLoadout> _loadouts =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a new registry pre-populated with the built-in loadouts.</summary>
    public ToolLoadoutRegistry()
    {
        RegisterBuiltIn();
    }

    /// <inheritdoc/>
    public void Register(ToolLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        _loadouts[loadout.Name] = loadout;
    }

    /// <inheritdoc/>
    public ToolLoadout? GetLoadout(string name) =>
        _loadouts.TryGetValue(name, out var loadout) ? loadout : null;

    /// <inheritdoc/>
    public IReadOnlyList<ToolLoadout> GetAll() => [.. _loadouts.Values];

    /// <inheritdoc/>
    public IReadOnlySet<string> ResolveActivePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins)
    {
        var plugins = availablePlugins.ToList();
        var (defaultPlugins, categories, disabled) = ResolveInheritedSettings(loadoutName);

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

            if (PluginCategoryMap.TryGetValue(plugin.Name, out var cat) &&
                categories.Contains(cat))
            {
                active.Add(plugin.Name);
            }
        }

        return active;
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> ResolveDiscoverablePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins)
    {
        var plugins = availablePlugins.ToList();
        var active = ResolveActivePlugins(loadoutName, plugins);
        var (_, _, disabled) = ResolveInheritedSettings(loadoutName);
        var patterns = ResolveDiscoverablePatterns(loadoutName);

        var discoverable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in plugins)
        {
            if (active.Contains(plugin.Name) || disabled.Contains(plugin.Name))
                continue;

            foreach (var pattern in patterns)
            {
                if (MatchesPattern(plugin.Name, pattern))
                {
                    discoverable.Add(plugin.Name);
                    break;
                }
            }
        }

        return discoverable;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Collects the effective DefaultPlugins, IncludedCategories, and DisabledPlugins
    /// by walking the parent chain from root to leaf, applying each layer in order.
    /// DisabledPlugins from any layer take precedence over inclusions from any layer.
    /// </summary>
    private (
        HashSet<string> DefaultPlugins,
        HashSet<ToolCategory> Categories,
        HashSet<string> Disabled)
        ResolveInheritedSettings(string loadoutName)
    {
        var chain = BuildInheritanceChain(loadoutName);
        var defaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categories = new HashSet<ToolCategory>();
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var loadout in chain)
        {
            foreach (var p in loadout.DefaultPlugins)
                defaultPlugins.Add(p);
            foreach (var c in loadout.IncludedCategories)
                categories.Add(c);
            foreach (var d in loadout.DisabledPlugins)
                disabled.Add(d);
        }

        return (defaultPlugins, categories, disabled);
    }

    private List<string> ResolveDiscoverablePatterns(string loadoutName)
    {
        var chain = BuildInheritanceChain(loadoutName);
        var patterns = new List<string>();
        foreach (var loadout in chain)
            patterns.AddRange(loadout.DiscoverablePatterns);
        return patterns;
    }

    /// <summary>
    /// Builds the inheritance chain <c>[root, …, leaf]</c> for the given loadout name.
    /// Cycles are silently broken using a visited set.
    /// </summary>
    private List<ToolLoadout> BuildInheritanceChain(string loadoutName)
    {
        var chain = new List<ToolLoadout>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = loadoutName;

        while (current is not null)
        {
            if (!visited.Add(current))
                break; // cycle guard

            if (!_loadouts.TryGetValue(current, out var loadout))
                break; // unknown loadout — stop

            chain.Add(loadout);
            current = loadout.ParentLoadoutName;
        }

        chain.Reverse(); // root-first application order
        return chain;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="pluginName"/> matches
    /// <paramref name="pattern"/>. Patterns ending with <c>*</c> are prefix matches;
    /// all others are exact case-insensitive matches.
    /// </summary>
    internal static bool MatchesPattern(string pluginName, string pattern)
    {
        if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            return pluginName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return pluginName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    // ── Built-in loadouts ────────────────────────────────────────────────────

    private void RegisterBuiltIn()
    {
        // Minimal — bare essentials; everything else is discoverable
        Register(ToolLoadoutBuilder
            .Create(WellKnownLoadouts.Minimal)
            .AddPlugin("think")
            .IncludeCategory(ToolCategory.Filesystem)
            .IncludeCategory(ToolCategory.Shell)
            .AddDiscoverable("*")
            .Build());

        // Developer — extends minimal with git, search, analysis, memory
        Register(ToolLoadoutBuilder
            .Create(WellKnownLoadouts.Developer)
            .Extends(WellKnownLoadouts.Minimal)
            .IncludeCategory(ToolCategory.Git)
            .IncludeCategory(ToolCategory.GitHub)
            .IncludeCategory(ToolCategory.Search)
            .IncludeCategory(ToolCategory.Analysis)
            .IncludeCategory(ToolCategory.Memory)
            .AddDiscoverable("docker*")
            .AddDiscoverable("mcp*")
            .Build());

        // Research — extends minimal with search, web, memory, multimodal
        Register(ToolLoadoutBuilder
            .Create(WellKnownLoadouts.Research)
            .Extends(WellKnownLoadouts.Minimal)
            .IncludeCategory(ToolCategory.Search)
            .IncludeCategory(ToolCategory.Web)
            .IncludeCategory(ToolCategory.Memory)
            .IncludeCategory(ToolCategory.Multimodal)
            .Build());

        // DevOps — extends minimal with git, network, scheduling
        Register(ToolLoadoutBuilder
            .Create(WellKnownLoadouts.DevOps)
            .Extends(WellKnownLoadouts.Minimal)
            .IncludeCategory(ToolCategory.Git)
            .IncludeCategory(ToolCategory.Network)
            .IncludeCategory(ToolCategory.Scheduling)
            .AddDiscoverable("docker*")
            .AddDiscoverable("kube*")
            .AddDiscoverable("terraform*")
            .Build());

        // Full — all categories loaded
        Register(ToolLoadoutBuilder
            .Create(WellKnownLoadouts.Full)
            .IncludeCategory(ToolCategory.Filesystem)
            .IncludeCategory(ToolCategory.Git)
            .IncludeCategory(ToolCategory.GitHub)
            .IncludeCategory(ToolCategory.Shell)
            .IncludeCategory(ToolCategory.Web)
            .IncludeCategory(ToolCategory.Search)
            .IncludeCategory(ToolCategory.Network)
            .IncludeCategory(ToolCategory.Memory)
            .IncludeCategory(ToolCategory.Orchestration)
            .IncludeCategory(ToolCategory.Analysis)
            .IncludeCategory(ToolCategory.Scheduling)
            .IncludeCategory(ToolCategory.Multimodal)
            .IncludeCategory(ToolCategory.Security)
            .Build());
    }
}
