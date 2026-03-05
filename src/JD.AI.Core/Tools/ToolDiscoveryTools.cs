using System.ComponentModel;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Allows agents to discover, search, and dynamically activate tools that are
/// not in their current loadout. This is the key mechanism enabling agents to
/// operate with a minimal toolset while still having access to the full tool
/// registry on demand — preventing context window overflow.
/// </summary>
public sealed class ToolDiscoveryTools
{
    private readonly Kernel _kernel;
    private readonly IToolLoadoutRegistry _registry;

    /// <summary>
    /// All plugins registered on the full kernel, including those not currently
    /// active. Populated at startup before loadout scoping is applied.
    /// </summary>
    private readonly IReadOnlyList<KernelPlugin> _allPlugins;

    public ToolDiscoveryTools(
        Kernel kernel,
        IToolLoadoutRegistry registry,
        IReadOnlyList<KernelPlugin> allPlugins)
    {
        _kernel = kernel;
        _registry = registry;
        _allPlugins = allPlugins;
    }

    [KernelFunction("discover_tools")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Search for available tools that are not currently loaded. Use this when you need a capability that isn't in your current toolset. Returns discoverable tools with descriptions, filterable by category or keyword, with pagination support.")]
    public string DiscoverTools(
        [Description("Filter by category (e.g. 'git', 'web', 'security', 'orchestration'). Omit for all.")] string? category = null,
        [Description("Search keyword to filter tool names and descriptions (case-insensitive).")] string? keyword = null,
        [Description("Page number (1-based, default 1). Each page shows 10 plugins.")] int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        // Get currently loaded plugin names
        var loadedNames = new HashSet<string>(
            _kernel.Plugins.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        // Find unloaded plugins
        var unloaded = _allPlugins
            .Where(p => !loadedNames.Contains(p.Name))
            .ToList();

        // Apply category filter
        if (category is not null &&
            Enum.TryParse<ToolCategory>(category, ignoreCase: true, out var cat))
        {
            unloaded = unloaded
                .Where(p => ToolLoadoutRegistry.PluginCategoryMap.TryGetValue(p.Name, out var pCat) && pCat == cat)
                .ToList();
        }

        // Apply keyword filter
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            unloaded = unloaded
                .Where(p =>
                    p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    p.Any(f =>
                        f.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        (f.Metadata.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)))
                .ToList();
        }

        var totalPlugins = unloaded.Count;
        var totalPages = Math.Max(1, (totalPlugins + pageSize - 1) / pageSize);
        page = Math.Min(page, totalPages);

        var pageItems = unloaded
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"## Discoverable Tools (page {page}/{totalPages}, {totalPlugins} plugins available)");
        sb.AppendLine();

        if (pageItems.Count == 0)
        {
            sb.AppendLine("No matching tools found.");
            if (category is not null || keyword is not null)
                sb.AppendLine("Try broadening your search or omitting filters.");
        }
        else
        {
            foreach (var plugin in pageItems)
            {
                var cat2 = ToolLoadoutRegistry.PluginCategoryMap.TryGetValue(plugin.Name, out var c)
                    ? c.ToString()
                    : "Uncategorized";
                var funcCount = plugin.Count();
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"### `{plugin.Name}` [{cat2}] ({funcCount} functions)");

                foreach (var fn in plugin.OrderBy(f => f.Name).Take(5))
                {
                    var desc = fn.Metadata.Description ?? "";
                    if (desc.Length > 80)
                        desc = string.Concat(desc.AsSpan(0, 77), "...");
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"  - `{fn.Name}`: {desc}");
                }

                if (funcCount > 5)
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"  - ... and {funcCount - 5} more");
                sb.AppendLine();
            }
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**Currently loaded**: {loadedNames.Count} plugins | **Available**: {totalPlugins} more");
        sb.AppendLine();
        sb.AppendLine("Use `activate_tool` to load a plugin into your current session.");

        return sb.ToString();
    }

    [KernelFunction("activate_tool")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Dynamically load a discoverable tool plugin into your current session. After activation, all functions in that plugin become available for use. Use discover_tools first to find available plugins.")]
    public string ActivateTool(
        [Description("Plugin name to activate (e.g. 'git', 'web', 'docker')")] string pluginName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);

        // Check if already loaded
        if (_kernel.Plugins.Any(p => p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Plugin '{pluginName}' is already loaded and available.";
        }

        // Find in the full registry
        var plugin = _allPlugins.FirstOrDefault(p =>
            p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));

        if (plugin is null)
        {
            return $"Error: Plugin '{pluginName}' not found. Use discover_tools to see available plugins.";
        }

        // Add to the kernel
        _kernel.Plugins.Add(plugin);

        var funcCount = plugin.Count();
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Activated plugin '{plugin.Name}' with {funcCount} functions:");
        foreach (var fn in plugin.OrderBy(f => f.Name))
        {
            var desc = fn.Metadata.Description ?? "";
            if (desc.Length > 60)
                desc = string.Concat(desc.AsSpan(0, 57), "...");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  - `{fn.Name}`: {desc}");
        }

        return sb.ToString();
    }

    [KernelFunction("list_tool_categories")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List all tool categories with the number of plugins in each. Helps you find the right category filter for discover_tools.")]
    public string ListCategories()
    {
        var loadedNames = new HashSet<string>(
            _kernel.Plugins.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("## Tool Categories");
        sb.AppendLine();

        foreach (var category in Enum.GetValues<ToolCategory>())
        {
            var pluginsInCat = _allPlugins
                .Where(p => ToolLoadoutRegistry.PluginCategoryMap.TryGetValue(p.Name, out var c) && c == category)
                .ToList();

            var loaded = pluginsInCat.Count(p => loadedNames.Contains(p.Name));
            var total = pluginsInCat.Count;
            var names = string.Join(", ", pluginsInCat.Select(p => p.Name).OrderBy(n => n));

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **{category}** ({loaded}/{total} loaded): {names}");
        }

        return sb.ToString();
    }

    [KernelFunction("list_toolbelts")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List available tool loadouts (toolbelts) and their configurations. Each loadout is a curated set of tools for a specific use case.")]
    public string ListToolbelts()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Available Tool Loadouts");
        sb.AppendLine();

        foreach (var loadout in _registry.GetAll())
        {
            var active = _registry.ResolveActivePlugins(loadout.Name, _allPlugins);
            var discoverable = _registry.ResolveDiscoverablePlugins(loadout.Name, _allPlugins);
            var parent = loadout.ParentLoadoutName is not null
                ? $" (extends {loadout.ParentLoadoutName})"
                : "";

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"### `{loadout.Name}`{parent}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  Active: {active.Count} plugins | Discoverable: {discoverable.Count} plugins");

            if (loadout.IncludedCategories.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  Categories: {string.Join(", ", loadout.IncludedCategories)}");
            if (loadout.DisabledPlugins.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  Disabled: {string.Join(", ", loadout.DisabledPlugins)}");

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
