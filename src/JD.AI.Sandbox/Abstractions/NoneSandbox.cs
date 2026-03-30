using System.Diagnostics;

namespace JD.AI.Sandbox.Abstractions;

/// <summary>
/// A no-op sandbox that runs processes without any OS-level isolation.
/// Used as fallback when platform-specific sandboxing is unavailable,
/// or for testing where isolation is not required.
/// </summary>
public sealed class NoneSandbox : ISandbox
{
    public SandboxPolicy Policy { get; }
    public SandboxPlatform Platform => SandboxPlatform.None;

    public NoneSandbox(SandboxPolicy policy)
    {
        Policy = policy;
    }

    /// <inheritdoc/>
    public async Task<SandboxedProcess> StartAsync(
        string executablePath,
        string arguments = "",
        CancellationToken ct = default)
    {
        var psi = BuildStartInfo(executablePath, arguments);
        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {executablePath}");

        return new SandboxedProcess(
            process,
            new StreamWriter(process.StandardInput.BaseStream, leaveOpen: true),
            new StreamReader(process.StandardOutput.BaseStream, leaveOpen: true),
            new StreamReader(process.StandardError.BaseStream, leaveOpen: true));
    }

    /// <inheritdoc/>
    public async Task<SandboxExecutionResult> RunAsync(
        string executablePath,
        string arguments = "",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var psi = BuildStartInfo(executablePath, arguments);
            var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start process: {executablePath}");

            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return new SandboxExecutionResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = output,
                StandardError = error,
                Elapsed = sw.Elapsed,
            };
        }
        catch (Exception ex)
        {
            return new SandboxExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Elapsed = sw.Elapsed,
                Error = ex.Message,
            };
        }
    }

    private ProcessStartInfo BuildStartInfo(string executablePath, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = Policy.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Merge policy environment with current environment
        if (Policy.EnvironmentVariables.Count > 0)
        {
            foreach (var kv in Policy.EnvironmentVariables)
            {
                if (kv.Value is null)
                    psi.Environment.Remove(kv.Key);
                else
                    psi.Environment[kv.Key] = kv.Value;
            }
        }

        return psi;
    }
}
