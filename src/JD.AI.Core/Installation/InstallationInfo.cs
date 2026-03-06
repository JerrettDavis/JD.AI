namespace JD.AI.Core.Installation;

/// <summary>
/// Describes the current JD.AI installation — how it was installed, where, and what version.
/// </summary>
/// <param name="Kind">How the tool was installed.</param>
/// <param name="ExecutablePath">Full path to the running executable.</param>
/// <param name="CurrentVersion">Currently running version string.</param>
/// <param name="RuntimeId">Runtime identifier for this platform (e.g. <c>win-x64</c>, <c>linux-arm64</c>).</param>
public sealed record InstallationInfo(
    InstallKind Kind,
    string ExecutablePath,
    string CurrentVersion,
    string RuntimeId);
