using System.Diagnostics;

namespace JD.AI.Tui.Tools.Sandbox;

/// <summary>
/// No sandboxing — direct process execution (current default behavior).
/// </summary>
public sealed class NoneSandbox : ISandbox
{
    public string ModeName => "none";

    public async Task<SandboxResult> ExecuteAsync(
        string command,
        string workingDirectory,
        int timeoutSeconds = 60,
        CancellationToken ct = default)
    {
        var isWindows = OperatingSystem.IsWindows();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

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
            return new SandboxResult(-1, "", "Command timed out.", TimedOut: true);
        }
    }
}
