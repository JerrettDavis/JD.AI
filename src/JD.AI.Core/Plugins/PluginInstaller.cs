using System.IO.Compression;
using JD.AI.Core.Config;
using JD.AI.Plugins.SDK;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Plugins;

/// <summary>
/// Installs plugin artifacts from local directories, package files, or URLs.
/// </summary>
public sealed class PluginInstaller : IPluginInstaller
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PluginInstaller> _logger;
    private readonly string _pluginsRoot;

    public PluginInstaller(
        HttpClient httpClient,
        ILogger<PluginInstaller> logger,
        string? pluginsRoot = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _pluginsRoot = pluginsRoot ?? Path.Combine(DataDirectories.Root, "plugins");
    }

    public async Task<PluginInstallArtifact> InstallAsync(string source, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var sourcePath = await MaterializeSourceAsync(source, ct).ConfigureAwait(false);
        var pluginRoot = await ExtractOrResolvePluginRootAsync(sourcePath, ct).ConfigureAwait(false);
        var manifestPath = Directory
            .EnumerateFiles(pluginRoot, "plugin.json", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (manifestPath is null)
        {
            throw new InvalidDataException("plugin.json was not found in the plugin package.");
        }

        var manifest = await PluginManifestReader.ReadAsync(manifestPath, ct).ConfigureAwait(false);
        var version = string.IsNullOrWhiteSpace(manifest.Version) ? "0.0.0" : manifest.Version;
        var installPath = Path.Combine(_pluginsRoot, manifest.Id, version);

        if (Directory.Exists(installPath))
        {
            Directory.Delete(installPath, recursive: true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);
        CopyDirectory(Path.GetDirectoryName(manifestPath)!, installPath);

        var entryAssemblyPath = ResolveEntryAssemblyPath(manifest, installPath);
        PluginIntegrityVerifier.VerifyEntryAssemblyHash(manifest, entryAssemblyPath);
        _logger.LogInformation(
            "Installed plugin package {Id} v{Version} to {InstallPath}",
            manifest.Id, version, installPath);

        return new PluginInstallArtifact(
            Manifest: manifest,
            InstallPath: installPath,
            EntryAssemblyPath: entryAssemblyPath,
            ManifestPath: Path.Combine(installPath, "plugin.json"),
            Source: source);
    }

    private async Task<string> MaterializeSourceAsync(string source, CancellationToken ct)
    {
        if (Directory.Exists(source) || File.Exists(source))
        {
            return source;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new FileNotFoundException($"Plugin source not found: {source}");
        }

        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".zip";
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"jdai-plugin-{Guid.NewGuid():N}{extension}");
        await using var network = await _httpClient.GetStreamAsync(uri, ct).ConfigureAwait(false);
        await using var file = File.Create(tempFile);
        await network.CopyToAsync(file, ct).ConfigureAwait(false);
        return tempFile;
    }

    private static Task<string> ExtractOrResolvePluginRootAsync(string sourcePath, CancellationToken ct)
    {
        if (Directory.Exists(sourcePath))
        {
            return Task.FromResult(sourcePath);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Plugin source must be a directory, .zip, or .nupkg.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-plugin-extract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        ZipFile.ExtractToDirectory(sourcePath, tempDir, overwriteFiles: true);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(tempDir);
    }

    private static string ResolveEntryAssemblyPath(PluginManifest manifest, string installPath)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var entry = Path.Combine(installPath, manifest.EntryAssembly);
            if (!File.Exists(entry))
            {
                throw new FileNotFoundException(
                    $"Entry assembly '{manifest.EntryAssembly}' not found in plugin package.",
                    entry);
            }

            return entry;
        }

        var firstDll = Directory
            .EnumerateFiles(installPath, "*.dll", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (firstDll is null)
        {
            throw new InvalidDataException(
                "Plugin package does not contain an entry assembly and manifest.entryAssembly is missing.");
        }

        return firstDll;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var target = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var target = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, target);
        }
    }
}
