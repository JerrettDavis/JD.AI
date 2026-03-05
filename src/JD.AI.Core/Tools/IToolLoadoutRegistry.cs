using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Registry for managing and resolving <see cref="ToolLoadout"/> definitions.
/// </summary>
public interface IToolLoadoutRegistry
{
    /// <summary>
    /// Registers a loadout. Overwrites any existing loadout with the same name.
    /// </summary>
    void Register(ToolLoadout loadout);

    /// <summary>Returns the loadout with the given name, or <see langword="null"/> if not found.</summary>
    ToolLoadout? GetLoadout(string name);

    /// <summary>Returns all registered loadouts in registration order.</summary>
    IReadOnlyList<ToolLoadout> GetAll();

    /// <summary>
    /// Resolves the set of plugin names that should be actively loaded for the given loadout,
    /// drawing from <paramref name="availablePlugins"/>. Respects inheritance, category
    /// mapping, and disabled-plugin overrides.
    /// </summary>
    /// <param name="loadoutName">Name of the loadout to resolve.</param>
    /// <param name="availablePlugins">All plugins registered on the kernel.</param>
    /// <returns>Plugin names that should be loaded for this loadout.</returns>
    IReadOnlySet<string> ResolveActivePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins);

    /// <summary>
    /// Resolves the set of plugin names that are discoverable (lazy) for the given loadout.
    /// These plugins are not loaded by default but can be enabled on request at runtime.
    /// Active and disabled plugins are excluded from the discoverable set.
    /// </summary>
    /// <param name="loadoutName">Name of the loadout to resolve.</param>
    /// <param name="availablePlugins">All plugins registered on the kernel.</param>
    /// <returns>Plugin names that are discoverable but not currently active.</returns>
    IReadOnlySet<string> ResolveDiscoverablePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins);
}
