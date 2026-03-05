using JD.AI.Core.Plugins;

namespace JD.AI.Tests.Plugins;

public sealed class PluginRegistryStoreTests
{
    [Fact]
    public async Task UpsertFindListRemove_RoundTripsRecord()
    {
        var dir = CreateTempDirectory();
        try
        {
            var store = new PluginRegistryStore(Path.Combine(dir, "registry.json"));
            var record = new InstalledPluginRecord
            {
                Id = "sample.plugin",
                Name = "Sample Plugin",
                Version = "1.0.0",
                InstallPath = Path.Combine(dir, "sample.plugin", "1.0.0"),
                EntryAssemblyPath = Path.Combine(dir, "sample.plugin", "1.0.0", "Sample.Plugin.dll"),
                ManifestPath = Path.Combine(dir, "sample.plugin", "1.0.0", "plugin.json"),
                Source = "./sample",
                Publisher = "acme",
                Permissions = ["service:*", "event:*"],
                Enabled = true,
            };

            await store.UpsertAsync(record);
            var found = await store.FindAsync("sample.plugin");
            var list = await store.ListAsync();

            Assert.NotNull(found);
            Assert.Single(list);
            Assert.Equal("Sample Plugin", found!.Name);
            Assert.Equal("acme", found.Publisher);
            Assert.Contains("service:*", found.Permissions);

            var removed = await store.RemoveAsync("sample.plugin");
            var empty = await store.ListAsync();

            Assert.True(removed);
            Assert.Empty(empty);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ListAsync_CorruptRegistry_ReturnsEmpty()
    {
        var dir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(dir, "registry.json");
            await File.WriteAllTextAsync(path, "{ definitely bad json");
            var store = new PluginRegistryStore(path);

            var list = await store.ListAsync();

            Assert.Empty(list);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jdai-plugin-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
