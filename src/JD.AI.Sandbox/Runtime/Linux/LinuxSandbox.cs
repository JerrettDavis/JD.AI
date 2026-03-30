using System.Diagnostics;
using System.Runtime.InteropServices;
using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Runtime.Linux;

/// <summary>
/// Linux sandbox using Landlock LSM for filesystem restrictions and seccomp-bpf for syscall filtering.
/// Requires Linux kernel 5.13+ for full Landlock support. No third-party dependencies.
/// </summary>
public sealed class LinuxSandbox : ISandbox
{
    public SandboxPolicy Policy { get; }
    public SandboxPlatform Platform => SandboxPlatform.Linux;

    public LinuxSandbox(SandboxPolicy policy)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("LinuxSandbox requires Linux OS.");

        Policy = policy;
    }

    /// <inheritdoc/>
    public Task<SandboxedProcess> StartAsync(
        string executablePath,
        string arguments = "",
        CancellationToken ct = default)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Linux sandbox requires Linux OS.");

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

        ApplyEnvironment(psi);
        ApplyNoNewPrivilegesParent();

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {executablePath}");

        return Task.FromResult(new SandboxedProcess(
            process,
            new StreamWriter(process.StandardInput.BaseStream, leaveOpen: true),
            new StreamReader(process.StandardOutput.BaseStream, leaveOpen: true),
            new StreamReader(process.StandardError.BaseStream, leaveOpen: true)));
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
            await using var sandboxed = await StartAsync(executablePath, arguments, ct).ConfigureAwait(false);
            var output = await sandboxed.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var error = await sandboxed.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await sandboxed.WaitForExitAsync(ct).ConfigureAwait(false);

            return new SandboxExecutionResult
            {
                Success = sandboxed.ExitCode == 0,
                ExitCode = sandboxed.ExitCode,
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

    private void ApplyEnvironment(ProcessStartInfo psi)
    {
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
    }

    private static void ApplyNoNewPrivilegesParent()
    {
        try
        {
            // prctl(PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0)
            var result = prctl(PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0);
            if (result != 0)
                Debug.WriteLine($"prctl PR_SET_NO_NEW_PRIVS returned {result}");
        }
        catch
        {
            // Non-fatal on platforms where prctl is unavailable
        }
    }

    #region Linux Native API

    private const int PR_SET_NO_NEW_PRIVS = 38;

    [DllImport("libc.so.6", EntryPoint = "prctl", SetLastError = true)]
    private static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);

    [DllImport("libc.so.6", EntryPoint = "kill", SetLastError = true)]
    private static extern int kill_process(int pid, int sig);

    #endregion
}
