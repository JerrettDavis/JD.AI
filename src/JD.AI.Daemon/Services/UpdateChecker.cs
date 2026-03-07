using System.Net.Http.Json;
using System.Reflection;
using JD.AI.Daemon.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JD.AI.Daemon.Services;

/// <summary>
/// Checks the NuGet feed for newer versions of the daemon package.
/// </summary>
public sealed class UpdateChecker
{
    private readonly UpdateConfig _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<UpdateChecker> _logger;

    public UpdateChecker(
        IOptions<UpdateConfig> config,
        IHttpClientFactory httpFactory,
        ILogger<UpdateChecker> logger)
    {
        _config = config.Value;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <summary>Current running version of the daemon.</summary>
    public Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Checks NuGet for the latest version and returns it, or null if already up-to-date.
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var latestVersion = await GetLatestVersionAsync(ct);
            if (latestVersion is null)
            {
                _logger.LogDebug("No versions found on NuGet for {PackageId}", _config.PackageId);
                return null;
            }

            if (latestVersion <= CurrentVersion)
            {
                _logger.LogDebug("Already up-to-date: {Current} >= {Latest}", CurrentVersion, latestVersion);
                return null;
            }

            _logger.LogInformation("Update available: {Current} → {Latest}", CurrentVersion, latestVersion);
            return new UpdateInfo(CurrentVersion, latestVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
            return null;
        }
    }

    private async Task<Version?> GetLatestVersionAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("NuGet");
        var packageId = _config.PackageId.ToLowerInvariant();
        var url = $"{_config.NuGetFeedUrl.TrimEnd('/')}/{packageId}/index.json";

        var response = await client.GetFromJsonAsync<NuGetVersionIndex>(url, ct);
        if (response?.Versions is null || response.Versions.Count == 0)
            return null;

        // Filter pre-release if not wanted
        var versions = response.Versions
            .Select(v => (Raw: v, Parsed: TryParseVersion(v)))
            .Where(v => v.Parsed is not null)
            .Where(v => _config.PreRelease || !v.Raw.Contains('-', StringComparison.Ordinal))
            .Select(v => v.Parsed!)
            .OrderDescending()
            .ToList();

        return versions.Count > 0 ? versions[0] : null;
    }

    private static Version? TryParseVersion(string version)
    {
        // Strip pre-release suffix for Version.Parse
        var dashIndex = version.IndexOf('-', StringComparison.Ordinal);
        var clean = dashIndex >= 0 ? version[..dashIndex] : version;
        return Version.TryParse(clean, out var v) ? v : null;
    }

    // Minimal model for NuGet v3 flatcontainer index.json
    private sealed record NuGetVersionIndex(IList<string> Versions);
}

/// <summary>Information about an available update.</summary>
public record UpdateInfo(Version CurrentVersion, Version LatestVersion)
{
    public override string ToString() => $"{CurrentVersion} → {LatestVersion}";
}
