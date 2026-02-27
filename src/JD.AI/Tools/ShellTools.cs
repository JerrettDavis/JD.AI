using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Tools;

/// <summary>
/// Shell execution tool for the AI agent.
/// </summary>
public sealed class ShellTools
{
    [KernelFunction("run_command")]
    [Description("Execute a shell command and return its output. Use for builds, tests, git operations, etc.")]
    public static async Task<string> RunCommandAsync(
        [Description("The command to execute")] string command,
        [Description("Working directory (defaults to cwd)")] string? cwd = null,
        [Description("Timeout in seconds (default 60)")] int timeoutSeconds = 60)
    {
        var workDir = cwd ?? Directory.GetCurrentDirectory();

        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return $"Error: Command timed out after {timeoutSeconds}s.\nPartial stdout:\n{stdout}\nPartial stderr:\n{stderr}";
        }

        var result = new StringBuilder();
        result.AppendLine($"Exit code: {process.ExitCode}");

        if (stdout.Length > 0)
        {
            result.AppendLine("--- stdout ---");
            result.Append(stdout);
        }

        if (stderr.Length > 0)
        {
            result.AppendLine("--- stderr ---");
            result.Append(stderr);
        }

        // Truncate very long output
        const int maxLength = 10000;
        var output = result.ToString();
        if (output.Length > maxLength)
        {
            output = string.Concat(output.AsSpan(0, maxLength), $"\n... [truncated, {output.Length - maxLength} more chars]");
        }

        return output;
    }
}
