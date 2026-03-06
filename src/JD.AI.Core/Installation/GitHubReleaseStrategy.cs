using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Installation;

/// <summary>
/// Downloads self-contained native binaries from GitHub Releases and replaces
/// the running executable in-place.
/// </summary>
public sealed class GitHubReleaseStrategy : IInstallStrategy
{
    private const string RepoOwner = "JerrettDavis";
    private const string RepoName = "JD.AI";
    private const string ApiBase = "https://api.github.com";

    private readonly string _rid;
    private readonly string _currentExePath;

    public GitHubReleaseStrategy(string runtimeId, string currentExePath)
    {
        _rid = runtimeId;
        _currentExePath = currentExePath;
    }

    public string Name => "GitHub release";

    public async Task<string?> GetLatestVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var release = await FetchLatestReleaseAsync(ct).ConfigureAwait(false);
            return release?.TagName?.TrimStart('v');
        }
#pragma warning disable CA1031
        catch { return null; }
#pragma warning restore CA1031
    }

    public async Task<InstallResult> ApplyAsync(string? targetVersion = null, CancellationToken ct = default)
    {
        // 1. Resolve the release (latest or specific version)
        GitHubRelease? release;
        if (targetVersion is not null)
        {
            release = await FetchReleaseByTagAsync($"v{targetVersion}", ct).ConfigureAwait(false)
                ?? await FetchReleaseByTagAsync(targetVersion, ct).ConfigureAwait(false);
        }
        else
        {
            release = await FetchLatestReleaseAsync(ct).ConfigureAwait(false);
        }

        if (release is null)
            return new InstallResult(false, "Could not find a release on GitHub.");

        // 2. Find the asset for our RID
        var assetName = GetExpectedAssetName();
        var asset = release.Assets?.FirstOrDefault(a =>
            a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            var available = release.Assets is { Count: > 0 }
                ? string.Join(", ", release.Assets.Select(a => a.Name))
                : "(none)";
            return new InstallResult(false,
                $"No binary for {_rid} in release {release.TagName}. Available: {available}");
        }

        // 3. Download to a temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"jdai-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var archivePath = Path.Combine(tempDir, asset.Name);
            await DownloadAssetAsync(asset.BrowserDownloadUrl, archivePath, ct).ConfigureAwait(false);

            // 4. Extract
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);

            if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, extractDir);
            }
            else if (asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractTarGzAsync(archivePath, extractDir, ct).ConfigureAwait(false);
            }
            else
            {
                return new InstallResult(false, $"Unsupported archive format: {asset.Name}");
            }

            // 5. Locate the jdai binary in extracted files
            var binaryName = OperatingSystem.IsWindows() ? "jdai.exe" : "jdai";
            var newBinary = Directory.EnumerateFiles(extractDir, binaryName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (newBinary is null)
                return new InstallResult(false,
                    $"Archive did not contain '{binaryName}'. Contents: " +
                    string.Join(", ", Directory.EnumerateFiles(extractDir, "*", SearchOption.AllDirectories)
                        .Select(Path.GetFileName)));

            // 6. Replace the running binary
            var installDir = Path.GetDirectoryName(_currentExePath)!;
            return await ReplaceExecutableAsync(newBinary, extractDir, installDir, ct).ConfigureAwait(false);
        }
        finally
        {
            // Best-effort cleanup
            try { Directory.Delete(tempDir, recursive: true); }
#pragma warning disable CA1031
            catch { /* temp dir cleanup is best-effort */ }
#pragma warning restore CA1031
        }
    }

    private string GetExpectedAssetName()
    {
        var ext = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
        return $"jdai-{_rid}.{ext}";
    }

    private async Task<InstallResult> ReplaceExecutableAsync(
        string newBinary, string extractDir, string installDir, CancellationToken ct)
    {
        var targetExe = Path.Combine(installDir, Path.GetFileName(newBinary));
        var backupExe = targetExe + ".old";

        // Remove previous backup if present
        if (File.Exists(backupExe))
        {
            try { File.Delete(backupExe); }
#pragma warning disable CA1031
            catch { /* may be locked on Windows — ignore */ }
#pragma warning restore CA1031
        }

        // Rename current → .old (works even on Windows for running executables)
        if (File.Exists(targetExe))
        {
            try
            {
                File.Move(targetExe, backupExe, overwrite: true);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                return new InstallResult(false,
                    $"Cannot replace running binary at {targetExe}: {ex.Message}");
            }
#pragma warning restore CA1031
        }

        // Move new binary into place
        File.Move(newBinary, targetExe, overwrite: true);

        // Copy all supporting files (if any) from the extracted archive
        foreach (var file in Directory.EnumerateFiles(extractDir, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(file, newBinary, StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(extractDir, file);
            var destPath = Path.Combine(installDir, relativePath);
            var destDir = Path.GetDirectoryName(destPath)!;
            Directory.CreateDirectory(destDir);
            File.Copy(file, destPath, overwrite: true);
        }

        // Set executable permissions on Unix
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                await Infrastructure.ProcessExecutor.RunAsync(
                    "chmod", $"+x \"{targetExe}\"",
                    timeout: TimeSpan.FromSeconds(5),
                    cancellationToken: ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch { /* chmod failure is non-fatal — binary may already be executable */ }
#pragma warning restore CA1031
        }

        return new InstallResult(true,
            $"Installed to {targetExe}",
            RequiresRestart: true);
    }

    // ── GitHub API helpers ───────────────────────────────────────

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("jdai-updater/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return http;
    }

    private static async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken ct)
    {
        using var http = CreateClient();
        return await http.GetFromJsonAsync<GitHubRelease>(
            $"{ApiBase}/repos/{RepoOwner}/{RepoName}/releases/latest", ct).ConfigureAwait(false);
    }

    private static async Task<GitHubRelease?> FetchReleaseByTagAsync(string tag, CancellationToken ct)
    {
        using var http = CreateClient();
        var response = await http.GetAsync(
            new Uri($"{ApiBase}/repos/{RepoOwner}/{RepoName}/releases/tags/{tag}"), ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: ct)
            .ConfigureAwait(false);
    }

    private static async Task DownloadAssetAsync(string url, string destPath, CancellationToken ct)
    {
        using var http = CreateClient();
        http.Timeout = TimeSpan.FromMinutes(10);
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");

        await using var responseStream = await http.GetStreamAsync(new Uri(url), ct).ConfigureAwait(false);
        await using var fileStream = File.Create(destPath);
        await responseStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
    }

    private static async Task ExtractTarGzAsync(string archivePath, string destDir, CancellationToken ct)
    {
        // Use system tar (available on Linux, macOS, and modern Windows)
        var result = await Infrastructure.ProcessExecutor.RunAsync(
            "tar", $"-xzf \"{archivePath}\" -C \"{destDir}\"",
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: ct).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(
                $"tar extraction failed: {result.StandardError}");
    }

    // ── DTOs ─────────────────────────────────────────────────────

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("size")] long Size);
}
