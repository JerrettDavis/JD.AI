// Licensed under the MIT License.

using System.Diagnostics;

namespace JD.AI.Core.Infrastructure;

/// <summary>
/// Result of a process execution containing exit code, stdout, and stderr.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Gets whether the process exited successfully (exit code 0).</summary>
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Unified process execution utility. Replaces duplicated ProcessStartInfo patterns
/// across GitTools, ShellTools, BrowserTools, ClipboardTools, GitHubTools, etc.
/// </summary>
public static class ProcessExecutor
{
    /// <summary>Default timeout for process execution.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs a process asynchronously, capturing stdout and stderr.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="timeout">Optional timeout (defaults to 30s).</param>
    /// <param name="standardInput">Optional text to write to stdin.</param>
    /// <param name="environmentVariables">Optional extra environment variables.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ProcessResult"/> with exit code, stdout, and stderr.</returns>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments = "",
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        string? standardInput = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (workingDirectory is not null)
            psi.WorkingDirectory = workingDirectory;

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
                psi.Environment[key] = value;
        }

        using var process = new Process { StartInfo = psi };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        try
        {
            process.Start();

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessResult(process.ExitCode, stdout.TrimEnd(), stderr.TrimEnd());
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            return new ProcessResult(-1, string.Empty, $"Process timed out after {effectiveTimeout.TotalSeconds:F0}s");
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw;
        }
    }

    /// <summary>
    /// Runs a process and returns only stdout, or an error-prefixed message on failure.
    /// Convenience method matching the common tool pattern.
    /// </summary>
    public static async Task<string> RunForOutputAsync(
        string fileName,
        string arguments = "",
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(fileName, arguments, workingDirectory, timeout, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.Success
            ? result.StandardOutput
            : $"Error (exit {result.ExitCode}): {result.StandardError}";
    }

    /// <summary>
    /// Opens a file or URL using the system shell (non-redirected).
    /// </summary>
    public static void ShellOpen(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });
    }

    /// <summary>
    /// Checks whether a CLI tool is available on the system PATH.
    /// </summary>
    public static async Task<bool> IsAvailableAsync(string toolName, CancellationToken cancellationToken = default)
    {
        var whichCommand = OperatingSystem.IsWindows() ? "where" : "which";
        var result = await RunAsync(whichCommand, toolName, timeout: TimeSpan.FromSeconds(5), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }
    }
}
