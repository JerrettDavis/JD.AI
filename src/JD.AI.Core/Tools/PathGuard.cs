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
    /// Returns <c>true</c> if the command string contains references to any protected directory.
    /// Used by ShellTools to reject commands that target protected paths.
    /// </summary>
    public static bool ContainsProtectedPath(string commandText)
    {
        var lower = commandText.ToLowerInvariant();

        // Check tilde-based paths
        if (lower.Contains("~/.openclaw"))
            return true;

        // Check absolute paths
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            var ocDir = Path.Combine(home, ".openclaw").ToLowerInvariant();
            if (lower.Contains(ocDir.Replace('\\', '/')) || lower.Contains(ocDir))
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
