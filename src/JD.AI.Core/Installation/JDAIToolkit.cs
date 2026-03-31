using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Installation;

/// <summary>
/// Describes a single installed JD.AI tool in the global toolkit.
/// </summary>
public sealed record InstalledTool(
    string PackageId,
    string ToolName,
    string CurrentVersion,
    InstallKind InstallKind);

/// <summary>
/// A complete update plan for all installed JD.AI tools.
/// </summary>
public sealed record UpdatePlan(
    IReadOnlyList<InstalledTool> Tools,
    IReadOnlyList<ToolUpdate> Updates,
    bool HasUpdates)
{
    public int TotalCount => Tools.Count;
    public int UpdateCount => Updates.Count;
    public int UpToDateCount => Tools.Count - Updates.Count;
}

/// <summary>
/// Individual tool update information.
/// </summary>
public sealed record ToolUpdate(
    InstalledTool Tool,
    string? LatestVersion,
    bool IsNewer)
{
    public string CurrentVersion => Tool.CurrentVersion;
    public string LatestVersionOrCurrent => LatestVersion ?? Tool.CurrentVersion;
}

/// <summary>
/// Provides unified multi-tool detection, version checking, and updating
/// for all JD.AI global tools installed via dotnet tool.
/// </summary>
public static class JDAIToolkit
{
    /// <summary>
    /// Detects all installed JD.AI dotnet global tools by parsing
    /// <c>dotnet tool list -g</c> output and filtering by known package prefixes.
    /// Falls back to checking each known package ID individually if the list is unavailable.
    /// </summary>
    public static async Task<IReadOnlyList<InstalledTool>> GetInstalledToolsAsync(
        CancellationToken ct = default)
    {
        var tools = new List<InstalledTool>();

        try
        {
            var result = await ProcessExecutor.RunAsync(
                "dotnet", "tool list -g",
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                tools.AddRange(ParseDotnetToolList(result.StandardOutput));
            }
        }
        catch
        {
            // Fall through to known-package probe
        }

        if (tools.Count == 0)
        {
            tools.AddRange(await ProbeKnownPackagesAsync(ct).ConfigureAwait(false));
        }

        return tools.AsReadOnly();
    }

    /// <summary>
    /// Checks all detected tools for available updates from NuGet, in parallel.
    /// Returns an <see cref="UpdatePlan"/> describing the current state of each tool.
    /// </summary>
    public static async Task<UpdatePlan> CheckAllAsync(
        IReadOnlyList<InstalledTool>? tools = null,
        CancellationToken ct = default)
    {
        tools ??= await GetInstalledToolsAsync(ct).ConfigureAwait(false);

        var tasks = tools.Select(async tool =>
        {
            var latest = await GetLatestVersionAsync(tool.PackageId, ct).ConfigureAwait(false);
            var isNewer = latest is not null && CompareVersions(latest, tool.CurrentVersion) > 0;
            return new ToolUpdate(tool, latest, isNewer);
        });

        var updates = await Task.WhenAll(tasks).ConfigureAwait(false);

        return new UpdatePlan(
            Tools: tools,
            Updates: updates.Where(u => u.IsNewer).ToList().AsReadOnly(),
            HasUpdates: updates.Any(u => u.IsNewer));
    }

    /// <summary>
    /// Applies updates for all tools that have a newer version available,
    /// in sequence. Stops on first failure unless <paramref name="continueOnError"/> is true.
    /// </summary>
    public static async Task ApplyAllAsync(
        UpdatePlan plan,
        bool continueOnError = false,
        Action<InstalledTool, InstallResult>? onToolUpdated = null,
        CancellationToken ct = default)
    {
        foreach (var update in plan.Updates)
        {
            var result = await ApplyAsync(update.Tool.PackageId, update.LatestVersion, ct)
                .ConfigureAwait(false);
            onToolUpdated?.Invoke(update.Tool, result);

            if (!result.Success && !continueOnError)
                break;
        }
    }

    /// <summary>
    /// Applies an update to a single tool by package ID.
    /// </summary>
    public static async Task<InstallResult> ApplyAsync(
        string packageId,
        string? targetVersion = null,
        CancellationToken ct = default)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var result = DetachedUpdater.Launch(packageId, targetVersion);
                return result.Success
                    ? new InstallResult(true,
                          $"Detached updater launched for {packageId}. Restart after completion.",
                          RequiresRestart: true,
                          LaunchedDetached: true)
                    : new InstallResult(false, $"Failed to launch updater: {result.Output}");
            }

            var versionArg = targetVersion is not null ? $" --version {targetVersion}" : "";
            var procResult = await ProcessExecutor.RunAsync(
                "dotnet",
                $"tool update -g {packageId}{versionArg}",
                timeout: TimeSpan.FromMinutes(3),
                cancellationToken: ct).ConfigureAwait(false);

            var output = string.IsNullOrWhiteSpace(procResult.StandardError)
                ? procResult.StandardOutput
                : $"{procResult.StandardOutput}\n{procResult.StandardError}";

            return new InstallResult(procResult.Success, output.Trim(), RequiresRestart: procResult.Success);
        }
        catch (Exception ex)
        {
            return new InstallResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Checks the latest version of a single package from NuGet.
    /// </summary>
    public static async Task<string?> GetLatestVersionAsync(
        string packageId,
        CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("jdai-updater/1.0");
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
            var response = await http.GetFromJsonAsync<NuGetVersionIndex>(url, ct)
                .ConfigureAwait(false);

            if (response?.Versions is null || response.Versions.Count == 0)
                return null;

            // Return latest stable version (no pre-release suffix)
            var latest = response.Versions
                .Select(v => (Raw: v, Clean: StripPreRelease(v)))
                .Where(v => !v.Raw.Contains('-'))
                .OrderByDescending(v => Version.Parse(v.Clean))
                .FirstOrDefault();

            return latest.Raw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Compares two semver-ish version strings. Returns positive if latest &gt; current.
    /// </summary>
    public static int CompareVersions(string latest, string current)
    {
        var latestVer = TryParseVersion(latest);
        var currentVer = TryParseVersion(current);
        if (latestVer is null) return -1;
        if (currentVer is null) return 1;
        return latestVer.CompareTo(currentVer);
    }

    // ── Private helpers ───────────────────────────────────────────

    private static IEnumerable<InstalledTool> ParseDotnetToolList(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Package", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith('-') ||
                string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var packageId = parts[0].Trim();
            var version = parts[1].Trim();

            if (!IsKnownPackage(packageId)) continue;

            var toolName = parts.Length >= 3 ? parts[2].Trim() : InferToolName(packageId);
            yield return new InstalledTool(packageId, toolName, version, InstallKind.DotnetTool);
        }
    }

    private static async Task<IReadOnlyList<InstalledTool>> ProbeKnownPackagesAsync(
        CancellationToken ct)
    {
        var results = new List<InstalledTool>();
        var packageIds = new[] { "JD.AI", "JD.AI.Daemon", "JD.AI.Gateway", "JD.AI.TUI" };

        foreach (var id in packageIds)
        {
            var showResult = await ProcessExecutor.RunAsync(
                "dotnet", $"tool show -g {id}",
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken: ct).ConfigureAwait(false);

            if (showResult.Success)
            {
                var version = ParseVersionFromToolShow(showResult.StandardOutput);
                if (version is not null)
                {
                    results.Add(new InstalledTool(id, InferToolName(id), version, InstallKind.DotnetTool));
                }
            }
        }

        return results.DistinctBy(t => t.PackageId).ToList().AsReadOnly();
    }

    private static string? ParseVersionFromToolShow(string output)
    {
        var match = Regex.Match(output, @"Version:\s*([^\s]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static bool IsKnownPackage(string packageId)
    {
        return packageId.Equals("JD.AI", StringComparison.OrdinalIgnoreCase) ||
               packageId.StartsWith("JD.AI.", StringComparison.OrdinalIgnoreCase);
    }

    private static string InferToolName(string packageId)
    {
        var suffix = packageId.Length > "JD.AI".Length
            ? packageId["JD.AI".Length..]
            : "";

        return string.IsNullOrEmpty(suffix)
            ? "jdai"
            : $"jdai{suffix.ToLowerInvariant().Replace(".", "-")}";
    }

    private static string StripPreRelease(string version)
    {
        var dash = version.IndexOf('-');
        return dash >= 0 ? version[..dash] : version;
    }

    private static Version? TryParseVersion(string v)
    {
        var clean = StripPreRelease(v);
        return Version.TryParse(clean, out var result) ? result : null;
    }

    private sealed record NuGetVersionIndex(
        [property: System.Text.Json.Serialization.JsonPropertyName("versions")]
        List<string> Versions);
}
