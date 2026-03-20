using System.Text.Json;

namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// Resolves OpenClaw state directory candidates and loads device identity/auth values.
/// </summary>
public static class OpenClawIdentityLoader
{
    private const string OpenClawStateDirEnv = "OPENCLAW_STATE_DIR";
    private const string JdAiOpenClawStateDirEnv = "JD_AI_OPENCLAW_STATE_DIR";

    public static string ResolveStateDir(string? configuredStateDir = null)
    {
        foreach (var candidate in EnumerateCandidates(configuredStateDir))
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home ?? ".", ".openclaw");
    }

    public static void LoadDeviceIdentity(OpenClawConfig config, string? configuredStateDir = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var stateDir = ResolveStateDir(configuredStateDir ?? config.OpenClawStateDir);
        config.OpenClawStateDir = stateDir;

        var devicePath = Path.Combine(stateDir, "identity", "device.json");
        var authPath = Path.Combine(stateDir, "identity", "device-auth.json");

        if (!File.Exists(devicePath) || !File.Exists(authPath))
            return;

        using (var deviceDoc = JsonDocument.Parse(File.ReadAllText(devicePath)))
        {
            var root = deviceDoc.RootElement;
            config.DeviceId = root.TryGetProperty("deviceId", out var deviceId) ? deviceId.GetString() ?? "" : "";
            config.PublicKeyPem = root.TryGetProperty("publicKeyPem", out var pub) ? pub.GetString() ?? "" : "";
            config.PrivateKeyPem = root.TryGetProperty("privateKeyPem", out var priv) ? priv.GetString() ?? "" : "";
        }

        using (var authDoc = JsonDocument.Parse(File.ReadAllText(authPath)))
        {
            var root = authDoc.RootElement;
            if (root.TryGetProperty("tokens", out var tokens)
                && tokens.TryGetProperty("operator", out var op)
                && op.TryGetProperty("token", out var token))
            {
                config.DeviceToken = token.GetString() ?? "";
            }
        }

        if (!string.IsNullOrWhiteSpace(config.GatewayToken))
            return;

        var configPath = Path.Combine(stateDir, "openclaw.json");
        if (!File.Exists(configPath))
            return;

        using var openClawDoc = JsonDocument.Parse(File.ReadAllText(configPath));
        if (openClawDoc.RootElement.TryGetProperty("gateway", out var gw)
            && gw.TryGetProperty("auth", out var gwAuth)
            && gwAuth.TryGetProperty("token", out var gwToken))
        {
            config.GatewayToken = gwToken.GetString() ?? "";
        }
    }

    public static bool HasRequiredIdentity(OpenClawConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return !string.IsNullOrWhiteSpace(config.DeviceId)
               && !string.IsNullOrWhiteSpace(config.PrivateKeyPem)
               && !string.IsNullOrWhiteSpace(config.PublicKeyPem)
               && (!string.IsNullOrWhiteSpace(config.DeviceToken) || !string.IsNullOrWhiteSpace(config.GatewayToken));
    }

    private static IEnumerable<string> EnumerateCandidates(string? configuredStateDir)
    {
        if (!string.IsNullOrWhiteSpace(configuredStateDir))
            yield return configuredStateDir.Trim();

        var envStateDir = Environment.GetEnvironmentVariable(OpenClawStateDirEnv);
        if (!string.IsNullOrWhiteSpace(envStateDir))
            yield return envStateDir.Trim();

        var jdAiEnvStateDir = Environment.GetEnvironmentVariable(JdAiOpenClawStateDirEnv);
        if (!string.IsNullOrWhiteSpace(jdAiEnvStateDir))
            yield return jdAiEnvStateDir.Trim();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, ".openclaw");

        foreach (var profileCandidate in EnumerateProfileCandidates())
            yield return profileCandidate;
    }

    private static IEnumerable<string> EnumerateProfileCandidates()
    {
        string? profilesRoot = null;
        if (OperatingSystem.IsWindows())
        {
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
            yield break;

        var candidates = new List<(string Path, DateTime LastWriteUtc)>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(profilesRoot))
            {
                var candidate = Path.Combine(dir, ".openclaw");
                var devicePath = Path.Combine(candidate, "identity", "device.json");
                var authPath = Path.Combine(candidate, "identity", "device-auth.json");
                if (!File.Exists(devicePath) || !File.Exists(authPath))
                    continue;

                var lastWrite = File.GetLastWriteTimeUtc(authPath);
                candidates.Add((candidate, lastWrite));
            }
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var (path, _) in candidates.OrderByDescending(c => c.LastWriteUtc))
            yield return path;
    }
}
