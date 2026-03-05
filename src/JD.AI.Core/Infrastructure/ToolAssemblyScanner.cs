// Licensed under the MIT License.

using System.Reflection;
using JD.AI.Core.Attributes;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

#pragma warning disable CA1034 // Do not nest type - ToolPluginDescriptor is intentionally nested for discoverability

namespace JD.AI.Core.Infrastructure;

/// <summary>
/// Scans assemblies for <see cref="ToolPluginAttribute"/> classes and builds
/// registration manifests and safety tier maps at startup. Replaces manual
/// <c>AddFromType</c> calls and the hardcoded tier dictionary.
/// </summary>
public static class ToolAssemblyScanner
{
    /// <summary>
    /// Represents a discovered tool plugin with its metadata.
    /// </summary>
    /// <param name="Type">The tool class type.</param>
    /// <param name="Attribute">The <see cref="ToolPluginAttribute"/> metadata.</param>
    public sealed record ToolPluginDescriptor(Type Type, ToolPluginAttribute Attribute);

    /// <summary>
    /// Discovers all types decorated with <see cref="ToolPluginAttribute"/> in the given assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan. If empty, scans the calling assembly.</param>
    /// <returns>Ordered list of plugin descriptors.</returns>
    public static IReadOnlyList<ToolPluginDescriptor> DiscoverPlugins(params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        return assemblies
            .SelectMany(a => a.GetTypes())
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<ToolPluginAttribute>()))
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Attr!.Order)
            .ThenBy(x => x.Attr!.Name, StringComparer.Ordinal)
            .Select(x => new ToolPluginDescriptor(x.Type, x.Attr!))
            .ToList();
    }

    /// <summary>
    /// Registers all discovered tool plugins that don't require constructor injection.
    /// Returns the list of plugins that need manual registration.
    /// </summary>
    /// <param name="kernel">The kernel to register plugins on.</param>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>Plugins that require manual injection-based registration.</returns>
    public static IReadOnlyList<ToolPluginDescriptor> RegisterStaticPlugins(
        Kernel kernel,
        params Assembly[] assemblies)
    {
        var plugins = DiscoverPlugins(assemblies);
        var needsInjection = new List<ToolPluginDescriptor>();

        foreach (var plugin in plugins)
        {
            if (plugin.Attribute.RequiresInjection)
            {
                needsInjection.Add(plugin);
                continue;
            }

            var kp = KernelPluginFactory.CreateFromType(plugin.Type, plugin.Attribute.Name);
            kernel.Plugins.Add(kp);
        }

        return needsInjection;
    }

    /// <summary>
    /// Builds a safety tier dictionary from <see cref="ToolSafetyTierAttribute"/> annotations
    /// on <c>[KernelFunction]</c> methods across the given assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>Dictionary mapping tool function names to their safety tiers.</returns>
    public static IReadOnlyDictionary<string, SafetyTier> BuildSafetyTierMap(params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        var map = new Dictionary<string, SafetyTier>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var kfAttr = method.GetCustomAttribute<KernelFunctionAttribute>();
                var stAttr = method.GetCustomAttribute<ToolSafetyTierAttribute>();

                if (kfAttr is null || stAttr is null)
                    continue;

                var functionName = kfAttr.Name ?? method.Name;
                map[functionName] = stAttr.Tier;
            }
        }

        return map;
    }

    /// <summary>
    /// Gets a summary of all discovered tools with their safety tiers.
    /// Useful for diagnostics and documentation generation.
    /// </summary>
    public static IReadOnlyList<(string PluginName, string FunctionName, SafetyTier Tier)> GetToolManifest(
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            assemblies = [Assembly.GetCallingAssembly()];

        var manifest = new List<(string, string, SafetyTier)>();

        foreach (var type in assemblies.SelectMany(a => a.GetTypes()))
        {
            var pluginAttr = type.GetCustomAttribute<ToolPluginAttribute>();
            if (pluginAttr is null)
                continue;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var kfAttr = method.GetCustomAttribute<KernelFunctionAttribute>();
                if (kfAttr is null)
                    continue;

                var stAttr = method.GetCustomAttribute<ToolSafetyTierAttribute>();
                var functionName = kfAttr.Name ?? method.Name;
                var tier = stAttr?.Tier ?? SafetyTier.AlwaysConfirm; // default to safest

                manifest.Add((pluginAttr.Name, functionName, tier));
            }
        }

        return manifest.OrderBy(x => x.Item1, StringComparer.Ordinal)
            .ThenBy(x => x.Item2, StringComparer.Ordinal)
            .ToList();
    }
}
