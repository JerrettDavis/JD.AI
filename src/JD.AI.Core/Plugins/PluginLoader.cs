using System.Reflection;
using System.Runtime.Loader;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Dynamically loads IJdAiPlugin implementations from assemblies.
/// Uses AssemblyLoadContext for isolation.
/// </summary>
public sealed class PluginLoader
    : IPluginRuntime
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly Lock _lock = new();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>Load all plugins from a directory.</summary>
    public async Task<IReadOnlyList<LoadedPlugin>> LoadFromDirectoryAsync(
        string directory, IPluginContext context, CancellationToken ct = default)
    {
        var loaded = new List<LoadedPlugin>();

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Plugin directory not found: {Dir}", directory);
            return loaded;
        }

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var plugin = await LoadAssemblyAsync(dll, context, ct: ct).ConfigureAwait(false);
                if (plugin is not null)
                    loaded.Add(plugin);
            }
#pragma warning disable CA1031 // non-fatal: log and continue loading other plugins
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from {Path}", dll);
            }
#pragma warning restore CA1031
        }

        return loaded;
    }

    /// <summary>Load a single plugin assembly.</summary>
    public async Task<LoadedPlugin?> LoadAssemblyAsync(
        string assemblyPath,
        IPluginContext context,
        string? pluginId = null,
        CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var alc = new PluginLoadContext(fullPath);
        var assembly = alc.LoadFromAssemblyPath(fullPath);

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IJdAiPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            _logger.LogDebug("No IJdAiPlugin types found in {Path}", assemblyPath);
            return null;
        }

        var pluginType = pluginTypes[0];
        var plugin = (IJdAiPlugin)Activator.CreateInstance(pluginType)!;

        var manifest = pluginType.GetCustomAttribute<JdAiPluginAttribute>();
        var resolvedId = pluginId ??
            manifest?.Id ??
            plugin.Id;

        await plugin.InitializeAsync(context, ct).ConfigureAwait(false);

        var loaded = new LoadedPlugin(
            Id: resolvedId,
            Plugin: plugin,
            Assembly: assembly,
            LoadContext: alc,
            AssemblyPath: assemblyPath,
            Name: manifest?.Name ?? plugin.Name,
            Version: plugin.Version,
            LoadedAt: DateTimeOffset.UtcNow);

        lock (_lock)
        {
            _plugins.RemoveAll(p =>
                string.Equals(p.Id, loaded.Id, StringComparison.OrdinalIgnoreCase));
            _plugins.Add(loaded);
        }

        _logger.LogInformation("Loaded plugin: {Name} v{Version} from {Path}",
            SanitizeForLog(loaded.Name), loaded.Version, SanitizeForLog(assemblyPath));

        return loaded;
    }

    /// <summary>Unload a plugin and its assembly.</summary>
    public async Task UnloadAsync(string nameOrId, CancellationToken ct = default)
    {
        LoadedPlugin? target;
        lock (_lock)
        {
            target = _plugins.FirstOrDefault(p =>
                string.Equals(p.Name, nameOrId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Id, nameOrId, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
                _plugins.Remove(target);
        }

        if (target is null) return;

        try
        {
            await target.Plugin.ShutdownAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // plugin faults are isolated by design
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin shutdown failed for {Name}", SanitizeForLog(target.Name));
        }
#pragma warning restore CA1031
        finally
        {
            target.LoadContext.Unload();
        }

        _logger.LogInformation("Unloaded plugin: {Name}", SanitizeForLog(nameOrId));
    }

    /// <summary>Get all loaded plugins.</summary>
    public IReadOnlyList<LoadedPlugin> GetAll()
    {
        lock (_lock)
            return [.. _plugins];
    }

    private static string SanitizeForLog(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}

/// <summary>Represents a loaded plugin with metadata.</summary>
public sealed record LoadedPlugin(
    string Id,
    IJdAiPlugin Plugin,
    Assembly Assembly,
    AssemblyLoadContext LoadContext,
    string AssemblyPath,
    string Name,
    string Version,
    DateTimeOffset LoadedAt);

/// <summary>Isolated load context for plugin assemblies.</summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedAssemblyPrefixes =
    [
        "JD.AI.Plugins.SDK",
        "Microsoft.SemanticKernel",
        "Microsoft.Extensions.AI",
        "Microsoft.Extensions.Logging",
        "System",
        "netstandard",
    ];

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (SharedAssemblyPrefixes.Any(prefix =>
            assemblyName.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true))
        {
            return null; // defer to host for shared dependencies
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : 0;
    }
}
