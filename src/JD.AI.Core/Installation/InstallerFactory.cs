namespace JD.AI.Core.Installation;

/// <summary>
/// Selects the appropriate <see cref="IInstallStrategy"/> based on detected installation kind.
/// </summary>
public static class InstallerFactory
{
    /// <summary>
    /// Creates an install strategy matching the given installation info.
    /// </summary>
    public static IInstallStrategy Create(InstallationInfo info) => info.Kind switch
    {
        InstallKind.DotnetTool => new DotnetToolStrategy(),

        InstallKind.Winget or InstallKind.Chocolatey or InstallKind.Scoop
            or InstallKind.Brew or InstallKind.Apt
            => new PackageManagerStrategy(info.Kind),

        // NativeBinary, Unknown — use GitHub releases as the universal fallback
        _ => new GitHubReleaseStrategy(info.RuntimeId, info.ExecutablePath),
    };
}
