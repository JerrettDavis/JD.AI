namespace JD.AI.Plugins.SDK;

/// <summary>
/// Base interface for JD.AI plugins. Plugins register SK functions,
/// tools, hooks, and configuration with the host.
/// </summary>
public interface IJdAiPlugin : IAsyncDisposable
{
    /// <summary>Unique plugin identifier (e.g., "jd.ai.plugin.github").</summary>
    string Id { get; }

    /// <summary>Human-readable plugin name.</summary>
    string Name { get; }

    /// <summary>Plugin version (SemVer).</summary>
    string Version { get; }

    /// <summary>Plugin description.</summary>
    string Description { get; }

    /// <summary>
    /// Called once when the plugin is loaded. Register SK functions,
    /// event handlers, and configuration here.
    /// </summary>
    Task InitializeAsync(IPluginContext context, CancellationToken ct = default);

    /// <summary>
    /// Called when the plugin is being unloaded.
    /// </summary>
    Task ShutdownAsync(CancellationToken ct = default);
}

/// <summary>
/// Context provided to plugins during initialization.
/// </summary>
public interface IPluginContext
{
    /// <summary>The SK kernel to register functions with.</summary>
    Microsoft.SemanticKernel.Kernel Kernel { get; }

    /// <summary>Plugin-specific configuration values.</summary>
    IReadOnlyDictionary<string, string> Configuration { get; }

    /// <summary>Registers a hook for a gateway event.</summary>
    void OnEvent(string eventType, Func<object?, Task> handler);

    /// <summary>Gets a service from the host DI container.</summary>
    T? GetService<T>() where T : class;

    /// <summary>Logs a message from the plugin.</summary>
    void Log(PluginLogLevel level, string message);
}

/// <summary>
/// Log levels for plugin messages.
/// </summary>
public enum PluginLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Attribute to mark a class as a JD.AI plugin for assembly scanning.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class JdAiPluginAttribute : Attribute
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Plugin manifest for distribution (plugin.json).
/// </summary>
public record PluginManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? License { get; init; }
    public string? EntryAssembly { get; init; }
    public IReadOnlyList<string> Permissions { get; init; } = [];
    public IReadOnlyDictionary<string, string> Configuration { get; init; } =
        new Dictionary<string, string>();
}
