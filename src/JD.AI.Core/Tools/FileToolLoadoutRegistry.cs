using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// An <see cref="IToolLoadoutRegistry"/> that discovers and loads
/// <c>*.loadout.yaml</c> files from one or more configured search paths.
/// </summary>
/// <remarks>
/// Files are loaded once at construction. Invalid or unreadable files are skipped
/// silently so that a single bad file does not prevent other loadouts from loading.
/// </remarks>
public sealed class FileToolLoadoutRegistry : IToolLoadoutRegistry
{
    private readonly Dictionary<string, ToolLoadout> _loadouts =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialises the registry by scanning all supplied paths for
    /// <c>*.loadout.yaml</c> files (recursive).
    /// </summary>
    public FileToolLoadoutRegistry(IEnumerable<string> searchPaths)
    {
        ArgumentNullException.ThrowIfNull(searchPaths);

        foreach (var path in searchPaths)
        {
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.loadout.yaml", SearchOption.AllDirectories))
            {
                try
                {
                    var loadout = ToolLoadoutYamlSerializer.DeserializeFile(file);
                    _loadouts[loadout.Name] = loadout;
                }
                catch (Exception)
                {
                    // Skip files that cannot be parsed — log in production if needed
                }
            }
        }
    }

    /// <summary>Number of loadouts successfully loaded from disk.</summary>
    public int LoadedCount => _loadouts.Count;

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
        IEnumerable<KernelPlugin> availablePlugins) =>
        LoadoutResolutionHelper.ResolveActivePlugins(loadoutName, availablePlugins, _loadouts);

    /// <inheritdoc/>
    public IReadOnlySet<string> ResolveDiscoverablePlugins(
        string loadoutName,
        IEnumerable<KernelPlugin> availablePlugins) =>
        LoadoutResolutionHelper.ResolveDiscoverablePlugins(loadoutName, availablePlugins, _loadouts);
}
