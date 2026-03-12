namespace JD.AI.Core.Tools;

/// <summary>
/// Guards file-system operations against accessing protected user data directories.
/// Any path that resolves into a protected directory is rejected.
/// </summary>
public static class PathGuard
{
    /// <summary>
    /// Directories under the user's home that are off-limits to AI file tools.
    /// Paths are resolved via <see cref="Path.GetFullPath"/> to defeat traversal.
    /// </summary>
    private static readonly Lazy<string[]> ProtectedDirs = new(() =>
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return [];

        return new[]
        {
            Path.GetFullPath(Path.Combine(home, ".openclaw")),
        };
    });

    /// <summary>
    /// Throws <see cref="PathGuardException"/> if <paramref name="path"/> resolves
    /// into a protected directory.
    /// </summary>
    public static void EnsureAllowed(string path)
    {
        if (IsProtected(path))
        {
            throw new PathGuardException(
                $"Access denied: '{path}' is inside a protected directory. " +
                "AI tools cannot read, write, or modify files in this location.");
        }
    }

    /// <summary>
    /// Command-text patterns that reference protected directories.
    /// Built once and cached. Used by <see cref="ContainsProtectedPath"/> and
    /// <see cref="Sandbox.RestrictedSandbox"/> for a single source of truth.
    /// </summary>
    internal static readonly Lazy<string[]> ProtectedCommandPatterns = new(() =>
    {
        var patterns = new List<string>
        {
            "~/.openclaw",
            "$HOME/.openclaw",
            "%USERPROFILE%\\.openclaw",
            ".openclaw/",
            ".openclaw\\",
        };

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            var ocDir = Path.Combine(home, ".openclaw");
            patterns.Add(ocDir.Replace('\\', '/').ToLowerInvariant());
            if (OperatingSystem.IsWindows())
                patterns.Add(ocDir.ToLowerInvariant());
        }

        return patterns.ToArray();
    });

    /// <summary>
    /// Returns <c>true</c> if the command string contains references to any protected directory.
    /// Used by ShellTools to reject commands that target protected paths.
    /// <para>
    /// <b>Known limitation:</b> This is a best-effort heuristic based on substring matching.
    /// It can be bypassed via shell variable expansion, backtick substitution, or encoding.
    /// The primary security boundary is <see cref="IsProtected"/> on direct file operations.
    /// </para>
    /// </summary>
    public static bool ContainsProtectedPath(string commandText)
    {
        foreach (var pattern in ProtectedCommandPatterns.Value)
        {
            if (commandText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the given path resolves into a protected directory.
    /// </summary>
    public static bool IsProtected(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return true; // Fail-closed: deny access to unresolvable paths
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var dir in ProtectedDirs.Value)
        {
            // Check if fullPath IS the directory or is INSIDE it
            if (fullPath.Equals(dir, comparison) ||
                fullPath.StartsWith(dir + Path.DirectorySeparatorChar, comparison) ||
                fullPath.StartsWith(dir + Path.AltDirectorySeparatorChar, comparison))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Thrown when an AI tool attempts to access a protected path.</summary>
public sealed class PathGuardException : InvalidOperationException
{
    public PathGuardException(string message) : base(message) { }

    public PathGuardException()
    {
    }

    public PathGuardException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
