using System.Net.Http.Json;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Installation;

/// <summary>
/// Updates JD.AI via <c>dotnet tool update -g JD.AI</c>.
/// </summary>
public sealed class DotnetToolStrategy : IInstallStrategy
{
    private const string PackageId = "JD.AI";
    private const string NuGetIndexUrl =
        "https://api.nuget.org/v3-flatcontainer/jd.ai/index.json";

    public string Name => "dotnet tool";

    public async Task<string?> GetLatestVersionAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("jdai-update/1.0");

            var response = await http
                .GetFromJsonAsync<NuGetVersionIndex>(NuGetIndexUrl, ct)
                .ConfigureAwait(false);

            return response?.Versions is { Count: > 0 } versions
                ? versions[^1]
                : null;
        }
#pragma warning disable CA1031
        catch { return null; }
#pragma warning restore CA1031
    }

    public async Task<InstallResult> ApplyAsync(string? targetVersion = null, CancellationToken ct = default)
    {
        var versionArg = targetVersion is not null ? $" --version {targetVersion}" : "";
        var result = await ProcessExecutor.RunAsync(
            "dotnet", $"tool update -g {PackageId}{versionArg}",
            timeout: TimeSpan.FromSeconds(120),
            cancellationToken: ct).ConfigureAwait(false);

        var output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : $"{result.StandardOutput}\n{result.StandardError}";

        return new InstallResult(result.Success, output.Trim(), RequiresRestart: result.Success);
    }

    private sealed record NuGetVersionIndex(
        [property: System.Text.Json.Serialization.JsonPropertyName("versions")]
        List<string> Versions);
}
