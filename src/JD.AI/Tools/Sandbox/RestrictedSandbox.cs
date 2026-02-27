using System.Diagnostics;

namespace JD.AI.Tui.Tools.Sandbox;

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

        var isWindows = OperatingSystem.IsWindows();
        var startInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Strip sensitive environment variables
        foreach (var key in Environment.GetEnvironmentVariables().Keys.Cast<string>())
        {
            if (SensitiveEnvPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                startInfo.Environment.Remove(key);
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var effectiveTimeout = Math.Min(timeoutSeconds, 30);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeout));

        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token).ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync(timeoutCts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            return new SandboxResult(process.ExitCode, output, error, TimedOut: false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); }
#pragma warning disable CA1031
            catch { /* best effort */ }
#pragma warning restore CA1031
            return new SandboxResult(-1, "", "Command timed out (restricted mode).", TimedOut: true);
        }
    }
}
