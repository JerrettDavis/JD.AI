using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Tools.Sandbox;

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
        var fileName = isWindows ? "cmd.exe" : "/bin/sh";
        var arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

        try
        {
            var result = await ProcessExecutor.RunAsync(
                fileName, arguments,
                workingDirectory: workingDirectory,
                timeout: TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken: ct).ConfigureAwait(false);

            return new SandboxResult(result.ExitCode, result.StandardOutput, result.StandardError, TimedOut: false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // ProcessExecutor handles timeout internally and returns exit -1
            return new SandboxResult(-1, "", "Command timed out.", TimedOut: true);
        }
    }
}
