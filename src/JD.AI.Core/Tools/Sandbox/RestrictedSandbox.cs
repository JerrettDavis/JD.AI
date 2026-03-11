using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Tools.Sandbox;

/// <summary>
/// Restricted sandbox — filters environment variables, blocks dangerous commands,
/// and enforces strict timeouts.
/// </summary>
public sealed class RestrictedSandbox : ISandbox
{
    private static readonly string[] SensitiveEnvPrefixes =
    [
        "AWS_", "AZURE_", "GCP_", "GOOGLE_",
        "GITHUB_TOKEN", "GH_TOKEN", "GITLAB_TOKEN",
        "NPM_TOKEN", "NUGET_API_KEY",
        "DATABASE_URL", "DB_",
        "SECRET_", "PRIVATE_KEY",
    ];

    private static readonly string[] BlockedPatterns =
    [
        "rm -rf /",
        "del /s /q c:\\",
        "format c:",
        ":(){:|:&};:",
        "mkfs",
        "dd if=",
        "shutdown",
        "reboot",
    ];

    private static string[] BuildProtectedPathPatterns()
    {
        var patterns = new List<string>
        {
            "~/.openclaw",
            ".openclaw/",
            ".openclaw\\",
        };

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            var ocDir = Path.Combine(home, ".openclaw");
            patterns.Add(ocDir.Replace('\\', '/'));
            if (OperatingSystem.IsWindows())
                patterns.Add(ocDir); // Backslash version
        }

        return patterns.ToArray();
    }

    private static readonly string[] ProtectedPathPatterns = BuildProtectedPathPatterns();

    public string ModeName => "restricted";

    public async Task<SandboxResult> ExecuteAsync(
        string command,
        string workingDirectory,
        int timeoutSeconds = 60,
        CancellationToken ct = default)
    {
        var lowerCmd = command.ToLowerInvariant();
        foreach (var pattern in BlockedPatterns)
        {
            if (lowerCmd.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return new SandboxResult(-1, "", $"Blocked: command matches dangerous pattern '{pattern}'", TimedOut: false);
            }
        }

        foreach (var pattern in ProtectedPathPatterns)
        {
            if (lowerCmd.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return new SandboxResult(-1, "", $"Blocked: command references protected directory '{pattern}'", TimedOut: false);
            }
        }

        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "cmd.exe" : "/bin/sh";
        var arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

        // Build environment with sensitive variables stripped
        var envVars = new Dictionary<string, string>();
        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (!SensitiveEnvPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                envVars[key] = Environment.GetEnvironmentVariable(key) ?? "";
            }
        }

        var effectiveTimeout = Math.Min(timeoutSeconds, 30);

        try
        {
            var result = await ProcessExecutor.RunAsync(
                fileName, arguments,
                workingDirectory: workingDirectory,
                timeout: TimeSpan.FromSeconds(effectiveTimeout),
                environmentVariables: envVars,
                clearEnvironment: true,
                cancellationToken: ct).ConfigureAwait(false);

            return new SandboxResult(result.ExitCode, result.StandardOutput, result.StandardError, TimedOut: false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new SandboxResult(-1, "", "Command timed out (restricted mode).", TimedOut: true);
        }
    }
}
