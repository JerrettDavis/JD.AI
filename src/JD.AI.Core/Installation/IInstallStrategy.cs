namespace JD.AI.Core.Installation;

/// <summary>
/// Strategy for checking and applying JD.AI updates based on installation method.
/// </summary>
public interface IInstallStrategy
{
    /// <summary>Human-readable name for this strategy (e.g. "dotnet tool", "GitHub release").</summary>
    string Name { get; }

    /// <summary>Fetches the latest available version from the upstream source. Returns null on failure.</summary>
    Task<string?> GetLatestVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies an update (or fresh install) to the specified version, or latest if null.
    /// </summary>
    Task<InstallResult> ApplyAsync(string? targetVersion = null, CancellationToken ct = default);
}

/// <summary>Result of an install or update operation.</summary>
/// <param name="Success">Whether the operation completed successfully.</param>
/// <param name="Output">Human-readable output describing what happened.</param>
/// <param name="RequiresRestart">Whether the user must restart <c>jdai</c> after applying.</param>
/// <param name="LaunchedDetached">
/// True when the update was launched as a detached background process (Windows self-update).
/// The caller should inform the user to exit and restart; the update will complete after the process exits.
/// </param>
public sealed record InstallResult(
    bool Success,
    string Output,
    bool RequiresRestart = false,
    bool LaunchedDetached = false);
