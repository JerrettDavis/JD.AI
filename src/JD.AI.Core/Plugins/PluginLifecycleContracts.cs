using JD.AI.Plugins.SDK;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Creates plugin contexts for plugin load/enable operations.
/// </summary>
public interface IPluginContextFactory
{
    IPluginContext CreateContext();
}

/// <summary>
/// Runtime abstraction for loading/unloading plugin assemblies.
/// </summary>
public interface IPluginRuntime
{
    Task<LoadedPlugin?> LoadAssemblyAsync(
        string assemblyPath,
        IPluginContext context,
        string? pluginId = null,
        CancellationToken ct = default);

    Task UnloadAsync(string nameOrId, CancellationToken ct = default);

    IReadOnlyList<LoadedPlugin> GetAll();
}

/// <summary>
/// Materializes plugin package sources (directory/file/url) into an installable artifact.
/// </summary>
public interface IPluginInstaller
{
    Task<PluginInstallArtifact> InstallAsync(string source, CancellationToken ct = default);
}

/// <summary>
/// High-level plugin lifecycle operations.
/// </summary>
public interface IPluginLifecycleManager
{
    Task<IReadOnlyList<PluginStatusInfo>> ListAsync(CancellationToken ct = default);

    Task<PluginStatusInfo?> GetAsync(string id, CancellationToken ct = default);

    Task<PluginStatusInfo> InstallAsync(
        string source,
        bool enable,
        CancellationToken ct = default);

    Task<PluginStatusInfo> EnableAsync(string id, CancellationToken ct = default);

    Task<PluginStatusInfo> DisableAsync(string id, CancellationToken ct = default);

    Task<PluginStatusInfo> UpdateAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<PluginStatusInfo>> UpdateAllAsync(CancellationToken ct = default);

    Task<bool> UninstallAsync(string id, CancellationToken ct = default);

    Task<int> LoadEnabledAsync(CancellationToken ct = default);
}

/// <summary>
/// Delegate-backed context factory for lightweight hosts and tests.
/// </summary>
public sealed class DelegatePluginContextFactory : IPluginContextFactory
{
    private readonly Func<IPluginContext> _factory;

    public DelegatePluginContextFactory(Func<IPluginContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IPluginContext CreateContext() => _factory();
}
