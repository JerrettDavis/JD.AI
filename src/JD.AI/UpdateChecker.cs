using System.Text.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Config;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Installation;

namespace JD.AI;

/// <summary>
/// Checks for newer versions of jdai and applies updates using the detected
/// installation strategy (dotnet tool, GitHub release, or package manager).
/// Caches results for 24 hours to avoid spamming upstream APIs on every startup.
/// </summary>
public static class UpdateChecker
{
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Indented;

    private static string CacheDir => DataDirectories.UpdateCacheDir;
    private static string CacheFile => Path.Combine(CacheDir, "update-check.json");

    /// <summary>Gets the running assembly's informational version.</summary>
    public static string GetCurrentVersion() => InstallationDetector.GetCurrentVersion();

    /// <summary>
    /// Checks for an available update, respecting the 24-hour cache.
    /// Returns null if no update is available or on any error (best-effort).
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(bool forceCheck = false, CancellationToken ct = default)
    {
        try
        {
            var current = GetCurrentVersion();

            // Check cache first
            if (!forceCheck)
            {
                var cached = ReadCache();
                if (cached is not null && DateTime.UtcNow - cached.LastCheck < CacheExpiry)
                {
                    return IsNewer(cached.LatestVersion, current)
                        ? new UpdateInfo(current, cached.LatestVersion)
                        : null;
                }
            }

            // Detect installation and query the appropriate source
            var info = await InstallationDetector.DetectAsync(ct).ConfigureAwait(false);
            var strategy = InstallerFactory.Create(info);
            var latest = await strategy.GetLatestVersionAsync(ct).ConfigureAwait(false);
            if (latest is null) return null;

            // Write cache
            WriteCache(new UpdateCache
            {
                LastCheck = DateTime.UtcNow,
                LatestVersion = latest,
                CurrentVersion = current,
            });

            return IsNewer(latest, current) ? new UpdateInfo(current, latest) : null;
        }
#pragma warning disable CA1031 // Do not catch general exception types — best-effort update check
        catch
#pragma warning restore CA1031
        {
            return null;
        }
    }

    /// <summary>
    /// Applies an update using the detected installation strategy.
    /// </summary>
    public static async Task<(bool Success, string Output)> ApplyUpdateAsync(CancellationToken ct = default)
    {
        var info = await InstallationDetector.DetectAsync(ct).ConfigureAwait(false);
        var strategy = InstallerFactory.Create(info);
        var result = await strategy.ApplyAsync(ct: ct).ConfigureAwait(false);
        return (result.Success, result.Output);
    }

    /// <summary>Compares two semver-ish version strings. Returns true if latest &gt; current.</summary>
    public static bool IsNewer(string latest, string current)
    {
        // Strip any pre-release suffixes for comparison
        static Version? Parse(string v)
        {
            var dashIdx = v.IndexOf('-', StringComparison.Ordinal);
            var clean = dashIdx >= 0 ? v[..dashIdx] : v;
            return Version.TryParse(clean, out var result) ? result : null;
        }

        var latestVer = Parse(latest);
        var currentVer = Parse(current);
        if (latestVer is null || currentVer is null) return false;
        return latestVer > currentVer;
    }

    private static UpdateCache? ReadCache()
    {
        if (!File.Exists(CacheFile)) return null;
        try
        {
            var json = File.ReadAllText(CacheFile);
            return JsonSerializer.Deserialize<UpdateCache>(json);
        }
#pragma warning disable CA1031
        catch { return null; }
#pragma warning restore CA1031
    }

    private static void WriteCache(UpdateCache cache)
    {
        Directory.CreateDirectory(CacheDir);
        var json = JsonSerializer.Serialize(cache, JsonOptions);
        File.WriteAllText(CacheFile, json);
    }
}

/// <summary>Cache file schema for update check results.</summary>
public sealed class UpdateCache
{
    [JsonPropertyName("lastCheck")]
    public DateTime LastCheck { get; set; }

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("currentVersion")]
    public string CurrentVersion { get; set; } = string.Empty;
}

/// <summary>Describes an available update.</summary>
public sealed record UpdateInfo(string CurrentVersion, string LatestVersion);
