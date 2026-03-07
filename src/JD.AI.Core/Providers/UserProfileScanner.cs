namespace JD.AI.Core.Providers;

/// <summary>
/// Scans user profile directories for credential files. Used by provider detectors
/// when running as a Windows service (LocalSystem) or systemd unit where the service
/// account's home directory does not contain user credentials.
/// </summary>
internal static class UserProfileScanner
{
    /// <summary>
    /// Determines whether the given home directory belongs to a service account
    /// rather than a real user.
    /// </summary>
    internal static bool IsServiceAccount(string homeDir)
    {
        if (OperatingSystem.IsWindows())
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return homeDir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(homeDir, "/root", StringComparison.Ordinal)
            || string.Equals(homeDir, "/", StringComparison.Ordinal)
            || string.IsNullOrEmpty(homeDir);
    }

    /// <summary>
    /// Scans all user profile directories for a relative file path.
    /// Returns the first existing match or <see langword="null"/>.
    /// </summary>
    /// <param name="relativePath">Path relative to the user's home (e.g. <c>.claude/.credentials.json</c>).</param>
    internal static string? FindInUserProfiles(string relativePath)
    {
        var profilesRoot = GetProfilesRoot();
        if (profilesRoot is null || !Directory.Exists(profilesRoot))
            return null;

        try
        {
            foreach (var userDir in Directory.EnumerateDirectories(profilesRoot))
            {
                var candidate = Path.Combine(userDir, relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return null;
    }

    /// <summary>
    /// Scans user profiles for a file relative to their <c>AppData\Local</c> directory (Windows only).
    /// </summary>
    internal static string? FindInUserLocalAppData(string relativePath)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var profilesRoot = GetProfilesRoot();
        if (profilesRoot is null || !Directory.Exists(profilesRoot))
            return null;

        try
        {
            foreach (var userDir in Directory.EnumerateDirectories(profilesRoot))
            {
                var candidate = Path.Combine(userDir, "AppData", "Local", relativePath);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return null;
    }

    private static string? GetProfilesRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            return Path.Combine(systemDrive, "Users");
        }

        if (OperatingSystem.IsLinux()) return "/home";
        if (OperatingSystem.IsMacOS()) return "/Users";

        return null;
    }
}
