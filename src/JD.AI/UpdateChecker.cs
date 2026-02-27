using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Tui;

/// <summary>
/// Checks NuGet for newer versions of the jdai tool and manages auto-update.
/// Caches results for 24 hours to avoid spamming the API on every startup.
/// </summary>
public static class UpdateChecker
{
    private const string PackageId = "JD.AI.Tui";
    private const string NuGetIndexUrl = "https://api.nuget.org/v3-flatcontainer/jd.ai.tui/index.json";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string CacheDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdai");

    private static string CacheFile => Path.Combine(CacheDir, "update-check.json");

    /// <summary>Gets the running assembly's informational version.</summary>
    public static string GetCurrentVersion()
    {
        var attr = typeof(UpdateChecker).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attr?.InformationalVersion is { } ver)
        {
            // Strip git metadata suffix (e.g. "0.1.42+abc1234" → "0.1.42")
            var plusIdx = ver.IndexOf('+', StringComparison.Ordinal);
            return plusIdx >= 0 ? ver[..plusIdx] : ver;
        }

        var asmVer = typeof(UpdateChecker).Assembly.GetName().Version;
        return asmVer?.ToString(3) ?? "0.0.0";
    }

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

            // Query NuGet
            var latest = await FetchLatestVersionAsync(ct).ConfigureAwait(false);
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
    /// Runs <c>dotnet tool update -g JD.AI.Tui</c> and returns the process output.
    /// </summary>
    public static async Task<(bool Success, string Output)> ApplyUpdateAsync(CancellationToken ct = default)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"tool update -g {PackageId}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return (false, "Failed to start dotnet process.");

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
        return (proc.ExitCode == 0, output.Trim());
    }

    /// <summary>Compares two semver-ish version strings. Returns true if latest > current.</summary>
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

    private static async Task<string?> FetchLatestVersionAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("jdai-update-checker/1.0");

        var response = await http.GetFromJsonAsync<NuGetVersionIndex>(NuGetIndexUrl, ct)
            .ConfigureAwait(false);

        // The index returns versions sorted ascending; last entry is the latest
        if (response?.Versions is { Count: > 0 } versions)
        {
            return versions[^1];
        }

        return null;
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

    private sealed class NuGetVersionIndex
    {
        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; } = [];
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
