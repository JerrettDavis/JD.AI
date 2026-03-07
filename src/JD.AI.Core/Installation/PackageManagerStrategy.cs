using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Installation;

/// <summary>
/// Updates JD.AI via system package managers (winget, chocolatey, scoop, brew, apt).
/// </summary>
public sealed class PackageManagerStrategy : IInstallStrategy
{
    private readonly InstallKind _kind;

    public PackageManagerStrategy(InstallKind kind)
    {
        if (kind is not (InstallKind.Winget or InstallKind.Chocolatey
                or InstallKind.Scoop or InstallKind.Brew or InstallKind.Apt))
        {
            throw new ArgumentException($"Not a package manager kind: {kind}", nameof(kind));
        }

        _kind = kind;
    }

    public string Name => _kind switch
    {
        InstallKind.Winget => "winget",
        InstallKind.Chocolatey => "chocolatey",
        InstallKind.Scoop => "scoop",
        InstallKind.Brew => "brew",
        InstallKind.Apt => "apt",
        _ => _kind.ToString().ToLowerInvariant(),
    };

    public async Task<string?> GetLatestVersionAsync(CancellationToken ct = default)
    {
        // Package managers handle version resolution themselves during upgrade.
        // We could parse their output, but it's not worth the fragility.
        // Delegate to the GitHub API as a universal version source.
        try
        {
            var ghStrategy = new GitHubReleaseStrategy(
                InstallationDetector.GetCurrentRid(),
                Environment.ProcessPath ?? "jdai");
            return await ghStrategy.GetLatestVersionAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch { return null; }
#pragma warning restore CA1031
    }

    public async Task<InstallResult> ApplyAsync(string? targetVersion = null, CancellationToken ct = default)
    {
        var (cmd, args) = GetUpgradeCommand(targetVersion);

        var result = await ProcessExecutor.RunAsync(
            cmd, args,
            timeout: TimeSpan.FromMinutes(5),
            cancellationToken: ct).ConfigureAwait(false);

        var output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : $"{result.StandardOutput}\n{result.StandardError}";

        return new InstallResult(result.Success, output.Trim(), RequiresRestart: result.Success);
    }

    private (string Command, string Args) GetUpgradeCommand(string? version) => _kind switch
    {
        InstallKind.Winget => ("winget", version is not null
            ? $"upgrade JD.AI --version {version}"
            : "upgrade JD.AI"),
        InstallKind.Chocolatey => ("choco", version is not null
            ? $"upgrade jdai --version {version} -y"
            : "upgrade jdai -y"),
        InstallKind.Scoop => ("scoop", "update jdai"),
        InstallKind.Brew => ("brew", "upgrade jdai"),
        InstallKind.Apt => ("sudo", version is not null
            ? $"apt-get install -y jdai={version}"
            : "apt-get install -y --only-upgrade jdai"),
        _ => throw new InvalidOperationException($"No upgrade command for {_kind}"),
    };
}
