using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using JD.AI.Core.Plugins;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Plugins;

public sealed class PluginInstallerTests
{
    [Fact]
    public async Task InstallAsync_DirectorySource_InstallsPlugin()
    {
        var sourceDir = CreateTempDirectory("source");
        var installDir = CreateTempDirectory("install");
        try
        {
            await CreatePluginDirectoryAsync(sourceDir, "sample.plugin", "1.0.0");
            var installer = new PluginInstaller(
                new HttpClient(new NeverCalledHttpHandler()),
                NullLogger<PluginInstaller>.Instance,
                installDir);

            var artifact = await installer.InstallAsync(sourceDir);

            Assert.Equal("sample.plugin", artifact.Manifest.Id);
            Assert.True(File.Exists(artifact.EntryAssemblyPath));
            Assert.True(File.Exists(Path.Combine(artifact.InstallPath, "plugin.json")));
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            Directory.Delete(installDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_ZipUrlSource_DownloadsAndInstallsPlugin()
    {
        var installDir = CreateTempDirectory("install");
        try
        {
            var zipBytes = BuildPluginZip("sample.plugin", "2.0.0");
            var client = new HttpClient(new InMemoryHttpHandler(zipBytes));
            var installer = new PluginInstaller(client, NullLogger<PluginInstaller>.Instance, installDir);

            var artifact = await installer.InstallAsync("https://plugins.example.com/sample.plugin.zip");

            Assert.Equal("2.0.0", artifact.Manifest.Version);
            Assert.True(File.Exists(artifact.EntryAssemblyPath));
            Assert.Contains("sample.plugin", artifact.InstallPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(installDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_MissingManifest_Throws()
    {
        var sourceDir = CreateTempDirectory("source");
        var installDir = CreateTempDirectory("install");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "Sample.Plugin.dll"), "stub");
            var installer = new PluginInstaller(
                new HttpClient(new NeverCalledHttpHandler()),
                NullLogger<PluginInstaller>.Instance,
                installDir);

            await Assert.ThrowsAsync<InvalidDataException>(() => installer.InstallAsync(sourceDir));
        }
        finally
        {
            Directory.Delete(sourceDir, recursive: true);
            Directory.Delete(installDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_UnknownSource_Throws()
    {
        var installDir = CreateTempDirectory("install");
        try
        {
            var installer = new PluginInstaller(
                new HttpClient(new NeverCalledHttpHandler()),
                NullLogger<PluginInstaller>.Instance,
                installDir);

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                installer.InstallAsync("this-source-does-not-exist"));
        }
        finally
        {
            Directory.Delete(installDir, recursive: true);
        }
    }

    private static async Task CreatePluginDirectoryAsync(string path, string id, string version)
    {
        var manifest = new PluginManifest
        {
            Id = id,
            Name = "Sample Plugin",
            Version = version,
            EntryAssembly = "Sample.Plugin.dll",
        };

        await File.WriteAllTextAsync(Path.Combine(path, "plugin.json"), System.Text.Json.JsonSerializer.Serialize(manifest));
        await File.WriteAllTextAsync(Path.Combine(path, "Sample.Plugin.dll"), "stub");
    }

    private static byte[] BuildPluginZip(string id, string version)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("plugin.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
            {
                var manifest = new PluginManifest
                {
                    Id = id,
                    Name = "Sample Plugin",
                    Version = version,
                    EntryAssembly = "Sample.Plugin.dll",
                };
                writer.Write(System.Text.Json.JsonSerializer.Serialize(manifest));
            }

            var dllEntry = archive.CreateEntry("Sample.Plugin.dll");
            using var dllWriter = new StreamWriter(dllEntry.Open(), Encoding.UTF8);
            dllWriter.Write("stub");
        }

        return memory.ToArray();
    }

    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"jdai-plugin-installer-{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class NeverCalledHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP should not be called in this test.");
    }

    private sealed class InMemoryHttpHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            return Task.FromResult(response);
        }
    }
}
