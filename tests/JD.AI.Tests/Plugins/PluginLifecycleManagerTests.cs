using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Plugins;

public sealed class PluginLifecycleManagerTests
{
    [Fact]
    public async Task InstallEnableDisableUninstall_FullLifecycle_Works()
    {
        var root = CreateTempDirectory();
        try
        {
            var recordDir = Path.Combine(root, "installed");
            Directory.CreateDirectory(recordDir);
            var entryAssembly = Path.Combine(recordDir, "Sample.Plugin.dll");
            await File.WriteAllTextAsync(entryAssembly, "stub");
            var manifestPath = Path.Combine(recordDir, "plugin.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(new PluginManifest
            {
                Id = "sample.plugin",
                Name = "Sample Plugin",
                Version = "1.2.3",
                Publisher = "test-publisher",
                EntryAssembly = "Sample.Plugin.dll",
            }));

            var manager = CreateManager(
                root,
                new FakeInstaller(new PluginInstallArtifact(
                    Manifest: new PluginManifest
                    {
                        Id = "sample.plugin",
                        Name = "Sample Plugin",
                        Version = "1.2.3",
                        EntryAssembly = "Sample.Plugin.dll",
                    },
                    InstallPath: recordDir,
                    EntryAssemblyPath: entryAssembly,
                    ManifestPath: manifestPath,
                    Source: "local")));

            var installed = await manager.InstallAsync("local", enable: true);
            var enabled = await manager.EnableAsync("sample.plugin");
            var disabled = await manager.DisableAsync("sample.plugin");
            var removed = await manager.UninstallAsync("sample.plugin");
            var after = await manager.GetAsync("sample.plugin");

            Assert.True(installed.Enabled);
            Assert.True(enabled.Enabled);
            Assert.False(disabled.Enabled);
            Assert.True(removed);
            Assert.Null(after);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnableAsync_UnknownPlugin_Throws()
    {
        var root = CreateTempDirectory();
        try
        {
            var manager = CreateManager(root, new FakeInstaller());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                manager.EnableAsync("unknown"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadEnabledAsync_LoadsOnlyEnabledPlugins()
    {
        var root = CreateTempDirectory();
        try
        {
            var registry = new PluginRegistryStore(Path.Combine(root, "registry.json"));
            var enabledEntry = CreateRecord(root, "enabled.plugin", enabled: true);
            var disabledEntry = CreateRecord(root, "disabled.plugin", enabled: false);
            await registry.UpsertAsync(enabledEntry);
            await registry.UpsertAsync(disabledEntry);

            var runtime = new FakeRuntime();
            var manager = new PluginLifecycleManager(
                new FakeInstaller(),
                registry,
                runtime,
                new DelegatePluginContextFactory(() => new NoopPluginContext()),
                NullLogger<PluginLifecycleManager>.Instance);

            var loadedCount = await manager.LoadEnabledAsync();
            var list = await manager.ListAsync();

            Assert.Equal(1, loadedCount);
            Assert.Single(runtime.LoadedIds, id =>
                string.Equals(id, "enabled.plugin", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(list, p =>
                string.Equals(p.Id, "enabled.plugin", StringComparison.OrdinalIgnoreCase) && p.Loaded);
            Assert.Contains(list, p =>
                string.Equals(p.Id, "disabled.plugin", StringComparison.OrdinalIgnoreCase) && !p.Loaded);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateAsync_ReinstallsFromRecordedSource_AndPreservesEnabledState()
    {
        var root = CreateTempDirectory();
        try
        {
            var installV1 = CreateInstallArtifact(root, "sample.plugin", "1.0.0", source: "catalog://sample.plugin");
            var installV2 = CreateInstallArtifact(root, "sample.plugin", "2.0.0", source: "catalog://sample.plugin");
            var manager = CreateManager(root, new FakeInstaller(installV1, installV2));

            var installed = await manager.InstallAsync("catalog://sample.plugin", enable: true);
            var updated = await manager.UpdateAsync("sample.plugin");

            Assert.Equal("1.0.0", installed.Version);
            Assert.Equal("2.0.0", updated.Version);
            Assert.True(updated.Enabled);
            Assert.Contains(Path.Combine("sample.plugin", "2.0.0"), updated.InstallPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_UntrustedPublisher_Throws()
    {
        var root = CreateTempDirectory();
        try
        {
            var artifact = CreateInstallArtifact(root, "sample.plugin", "1.0.0", source: "catalog://sample.plugin");
            var manager = CreateManager(
                root,
                new FakeInstaller(artifact),
                new PluginSecurityOptions
                {
                    TrustedPublishers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "trusted-publisher",
                    },
                });

            var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
                manager.InstallAsync("catalog://sample.plugin", enable: true));

            Assert.Contains("publisher", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("trusted-publisher", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PluginLifecycleManager CreateManager(
        string root,
        IPluginInstaller installer,
        PluginSecurityOptions? securityOptions = null)
    {
        var registry = new PluginRegistryStore(Path.Combine(root, "registry.json"));
        return new PluginLifecycleManager(
            installer,
            registry,
            new FakeRuntime(),
            new DelegatePluginContextFactory(() => new NoopPluginContext()),
            NullLogger<PluginLifecycleManager>.Instance,
            securityOptions);
    }

    private static InstalledPluginRecord CreateRecord(string root, string id, bool enabled)
    {
        var pluginDir = Path.Combine(root, id, "1.0.0");
        Directory.CreateDirectory(pluginDir);
        var entryAssemblyPath = Path.Combine(pluginDir, "Sample.Plugin.dll");
        File.WriteAllText(entryAssemblyPath, "stub");
        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new PluginManifest
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            Publisher = "test-publisher",
            EntryAssembly = "Sample.Plugin.dll",
        }));

        return new InstalledPluginRecord
        {
            Id = id,
            Name = id,
            Version = "1.0.0",
            InstallPath = pluginDir,
            EntryAssemblyPath = entryAssemblyPath,
            ManifestPath = manifestPath,
            Source = "test",
            Enabled = enabled,
        };
    }

    private static PluginInstallArtifact CreateInstallArtifact(
        string root,
        string id,
        string version,
        string source)
    {
        var installPath = Path.Combine(root, "installed", id, version);
        Directory.CreateDirectory(installPath);

        var entryAssembly = Path.Combine(installPath, "Sample.Plugin.dll");
        File.WriteAllText(entryAssembly, "stub");

        var manifestPath = Path.Combine(installPath, "plugin.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new PluginManifest
        {
            Id = id,
            Name = "Sample Plugin",
            Version = version,
            Publisher = "test-publisher",
            EntryAssembly = "Sample.Plugin.dll",
        }));

        return new PluginInstallArtifact(
            Manifest: new PluginManifest
            {
                Id = id,
                Name = "Sample Plugin",
                Version = version,
                Publisher = "test-publisher",
                EntryAssembly = "Sample.Plugin.dll",
            },
            InstallPath: installPath,
            EntryAssemblyPath: entryAssembly,
            ManifestPath: manifestPath,
            Source: source);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jdai-plugin-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeInstaller : IPluginInstaller
    {
        private readonly Queue<PluginInstallArtifact> _artifacts;

        public FakeInstaller(params PluginInstallArtifact[] artifacts)
        {
            _artifacts = new Queue<PluginInstallArtifact>(artifacts);
        }

        public Task<PluginInstallArtifact> InstallAsync(string source, CancellationToken ct = default)
        {
            if (_artifacts.Count == 0)
            {
                throw new InvalidOperationException("No fake artifact configured.");
            }

            return Task.FromResult(_artifacts.Dequeue());
        }
    }

    private sealed class FakeRuntime : IPluginRuntime
    {
        private readonly List<string> _loadedIds = [];
        public IReadOnlyList<string> LoadedIds => _loadedIds;

        public Task<LoadedPlugin?> LoadAssemblyAsync(
            string assemblyPath,
            IPluginContext context,
            string? pluginId = null,
            CancellationToken ct = default)
        {
            var id = pluginId ?? Path.GetFileNameWithoutExtension(assemblyPath);
            if (!_loadedIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                _loadedIds.Add(id);
            }

            return Task.FromResult<LoadedPlugin?>(new LoadedPlugin(
                Id: id,
                Plugin: new NoopPlugin(),
                Assembly: Assembly.GetExecutingAssembly(),
                LoadContext: AssemblyLoadContext.Default,
                AssemblyPath: assemblyPath,
                Name: id,
                Version: "1.0.0",
                LoadedAt: DateTimeOffset.UtcNow));
        }

        public Task UnloadAsync(string nameOrId, CancellationToken ct = default)
        {
            _loadedIds.RemoveAll(id =>
                string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }

        public IReadOnlyList<LoadedPlugin> GetAll()
        {
            return _loadedIds
                .Select(id => new LoadedPlugin(
                    Id: id,
                    Plugin: new NoopPlugin(),
                    Assembly: Assembly.GetExecutingAssembly(),
                    LoadContext: AssemblyLoadContext.Default,
                    AssemblyPath: $"{id}.dll",
                    Name: id,
                    Version: "1.0.0",
                    LoadedAt: DateTimeOffset.UtcNow))
                .ToList();
        }
    }

    private sealed class NoopPluginContext : IPluginContext
    {
        public Kernel Kernel { get; } = new();
        public IReadOnlyDictionary<string, string> Configuration { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public void OnEvent(string eventType, Func<object?, Task> handler) { }
        public T? GetService<T>() where T : class => null;
        public void Log(PluginLogLevel level, string message) { }
    }

    private sealed class NoopPlugin : IJdAiPlugin
    {
        public string Id => "noop";
        public string Name => "Noop";
        public string Version => "1.0.0";
        public string Description => "Noop";
        public Task InitializeAsync(IPluginContext context, CancellationToken ct = default) => Task.CompletedTask;
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
