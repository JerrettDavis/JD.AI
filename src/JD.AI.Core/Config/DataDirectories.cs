namespace JD.AI.Core.Config;

/// <summary>
/// Centralized data directory resolution for JD.AI.
/// When running as a Windows Service (LocalSystem) or systemd unit (root),
/// <c>Environment.SpecialFolder.UserProfile</c> resolves to the service account's
/// profile rather than any real user folder. This class scans all user profiles
/// and falls back to ProgramData / /var/lib when no user-level .jdai directory
/// exists.
/// </summary>
public static class DataDirectories
{
    private const string AppDirName = ".jdai";

    private static string? _resolvedRoot;
    private static readonly Lock Lock = new();

    /// <summary>
    /// Root data directory (e.g. <c>C:\Users\jd\.jdai</c> or <c>/home/jd/.jdai</c>).
    /// Resolution order:
    /// <list type="number">
    ///   <item>Environment variable <c>JDAI_DATA_DIR</c> (explicit override)</item>
    ///   <item>Current user's profile <c>~/.jdai</c> (if it exists or is writable)</item>
    ///   <item>Scan all user profiles for an existing <c>.jdai</c> directory</item>
    ///   <item>Machine-level fallback: <c>%ProgramData%\JD.AI</c> or <c>/var/lib/jdai</c></item>
    /// </list>
    /// </summary>
    public static string Root
    {
        get
        {
            if (_resolvedRoot is not null)
                return _resolvedRoot;

            lock (Lock)
            {
#pragma warning disable CA1508 // Double-check pattern: _resolvedRoot may be set by another thread
                if (_resolvedRoot is null)
                    _resolvedRoot = Resolve();
#pragma warning restore CA1508
            }

            return _resolvedRoot;
        }
    }

    /// <summary>Path to the SQLite session database.</summary>
    public static string SessionsDb => Path.Combine(Root, "sessions.db");

    /// <summary>Path to the SQLite vector store database.</summary>
    public static string VectorsDb => Path.Combine(Root, "vectors.db");

    /// <summary>Path to the update-check cache file.</summary>
    public static string UpdateCacheDir => Root;

    /// <summary>Path to the OpenClaw workspace directory for a given agent.</summary>
    public static string OpenClawWorkspace(string agentId) =>
        Path.Combine(Root, "openclaw-workspaces", agentId);

    /// <summary>
    /// Path to organization config repository. Set via JDAI_ORG_CONFIG environment variable
    /// or by writing the path to ~/.jdai/org-config-path.
    /// </summary>
    public static string? OrgConfigPath
    {
        get
        {
            var envPath = Environment.GetEnvironmentVariable("JDAI_ORG_CONFIG");
            if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
                return envPath;

            var configFile = Path.Combine(Root, "org-config-path");
            if (File.Exists(configFile))
            {
                try
                {
                    var storedPath = File.ReadAllText(configFile).Trim();
                    if (Directory.Exists(storedPath))
                        return storedPath;
                }
                catch (IOException) { /* Treat unreadable org config as "no org config". */ }
                catch (UnauthorizedAccessException) { /* Treat permission issues as "no org config". */ }
            }

            return null;
        }
    }

    /// <summary>
    /// Allow runtime override (e.g. from appsettings.json or tests).
    /// Must be called before first access of <see cref="Root"/>.
    /// </summary>
    public static void SetRoot(string path)
    {
        lock (Lock)
        {
            _resolvedRoot = path;
        }
    }

    /// <summary>Reset cached root (primarily for testing).</summary>
    internal static void Reset()
    {
        lock (Lock)
        {
            _resolvedRoot = null;
        }
    }

    private static string Resolve()
    {
        // 1. Explicit environment variable override
        var envDir = Environment.GetEnvironmentVariable("JDAI_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            Directory.CreateDirectory(envDir);
            return envDir;
        }

        // 2. Current user's home directory
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userHome))
        {
            var userDir = Path.Combine(userHome, AppDirName);
            if (Directory.Exists(userDir))
                return userDir;

            // If running as a normal user, create and use their home dir
            if (!IsServiceAccount(userHome))
            {
                Directory.CreateDirectory(userDir);
                return userDir;
            }
        }

        // 3. Scan user profiles for an existing .jdai directory
        var scanned = ScanUserProfiles();
        if (scanned is not null)
            return scanned;

        // 4. Machine-level fallback
        var fallback = GetMachineFallback();
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool IsServiceAccount(string homeDir)
    {
        if (OperatingSystem.IsWindows())
        {
            // LocalSystem resolves to C:\Windows\system32\config\systemprofile
            // LocalService  -> C:\Windows\ServiceProfiles\LocalService
            // NetworkService -> C:\Windows\ServiceProfiles\NetworkService
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return homeDir.StartsWith(winDir, StringComparison.OrdinalIgnoreCase);
        }

        // Linux/macOS: root or nobody
        return string.Equals(homeDir, "/root", StringComparison.Ordinal)
            || string.Equals(homeDir, "/", StringComparison.Ordinal)
            || string.IsNullOrEmpty(homeDir);
    }

    private static string? ScanUserProfiles()
    {
        string? profilesRoot = null;

        if (OperatingSystem.IsWindows())
        {
            // Typical: C:\Users
            var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            profilesRoot = Path.Combine(systemDrive, "Users");
        }
        else if (OperatingSystem.IsLinux())
        {
            profilesRoot = "/home";
        }
        else if (OperatingSystem.IsMacOS())
        {
            profilesRoot = "/Users";
        }

        if (profilesRoot is null || !Directory.Exists(profilesRoot))
            return null;

        try
        {
            foreach (var userDir in Directory.EnumerateDirectories(profilesRoot))
            {
                var candidate = Path.Combine(userDir, AppDirName);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Can't enumerate user profiles — skip
        }

        return null;
    }

    private static string GetMachineFallback()
    {
        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "JD.AI");
        }

        // Linux/macOS
        return "/var/lib/jdai";
    }
}
