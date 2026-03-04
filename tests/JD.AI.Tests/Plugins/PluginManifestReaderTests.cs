using System.Text.Json;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;

namespace JD.AI.Tests.Plugins;

public sealed class PluginManifestReaderTests
{
    [Fact]
    public async Task ReadAsync_ValidManifest_ReturnsManifest()
    {
        var dir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(dir, "plugin.json");
            var manifest = new PluginManifest
            {
                Id = "sample.plugin",
                Name = "Sample Plugin",
                Version = "1.0.0",
                EntryAssembly = "Sample.Plugin.dll",
            };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest));

            var result = await PluginManifestReader.ReadAsync(path);

            Assert.Equal("sample.plugin", result.Id);
            Assert.Equal("Sample Plugin", result.Name);
            Assert.Equal("1.0.0", result.Version);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            PluginManifestReader.ReadAsync(@"C:\definitely-not-present\plugin.json"));
    }

    [Fact]
    public void Validate_MissingId_Throws()
    {
        var manifest = new PluginManifest
        {
            Id = "",
            Name = "Sample Plugin",
            Version = "1.0.0",
        };

        var ex = Assert.Throws<InvalidDataException>(() =>
            PluginManifestReader.Validate(manifest, "plugin.json"));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_InvalidId_Throws()
    {
        var manifest = new PluginManifest
        {
            Id = "bad/id",
            Name = "Sample Plugin",
            Version = "1.0.0",
        };

        Assert.Throws<InvalidDataException>(() =>
            PluginManifestReader.Validate(manifest, "plugin.json"));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jdai-plugin-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
