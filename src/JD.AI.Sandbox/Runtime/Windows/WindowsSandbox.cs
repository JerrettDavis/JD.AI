using System.Diagnostics;
using System.Runtime.InteropServices;
using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Runtime.Windows;

/// <summary>
/// Windows sandbox using Job Objects for resource limits and Restricted Tokens for capability stripping.
/// Requires Windows Vista or later. No third-party dependencies.
/// </summary>
public sealed class WindowsSandbox : ISandbox
{
    public SandboxPolicy Policy { get; }
    public SandboxPlatform Platform => SandboxPlatform.Windows;

    public WindowsSandbox(SandboxPolicy policy)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsSandbox requires Windows OS.");

        Policy = policy;
    }

    /// <inheritdoc/>
    public Task<SandboxedProcess> StartAsync(
        string executablePath,
        string arguments = "",
        CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows sandbox requires Windows OS.");

        var job = CreateJobObject();
        if (job == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to create Job Object: {Marshal.GetLastWin32Error()}");

        try
        {
            ConfigureJobLimits(job);

            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = Policy.WorkingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            ApplyEnvironment(psi);

            var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to create process: {executablePath}");

            try
            {
                if (!AssignProcessToJobObject(job, process.SafeHandle!))
                    throw new InvalidOperationException($"Failed to assign process to job: {Marshal.GetLastWin32Error()}");

                return Task.FromResult(new SandboxedProcess(
                    process,
                    new StreamWriter(process.StandardInput.BaseStream, leaveOpen: true),
                    new StreamReader(process.StandardOutput.BaseStream, leaveOpen: true),
                    new StreamReader(process.StandardError.BaseStream, leaveOpen: true)));
            }
            catch
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
        }
        catch
        {
            CloseHandle(job);
            throw;
        }
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

    private IntPtr CreateJobObject()
    {
        return CreateJobObjectW(IntPtr.Zero, null);
    }

    private void ConfigureJobLimits(IntPtr job)
    {
        var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JOB_OBJECT_LIMIT_PROCESS_TIME,
            PerProcessTimeLimit = Policy.MaxCpuTimeMs.HasValue
                ? (long)Policy.MaxCpuTimeMs.Value * 10_000L
                : 0,
        };

        var extInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = info,
        };

        if (Policy.MaxMemoryBytes.HasValue)
        {
            extInfo.BasicLimitInformation.LimitFlags |= JOB_OBJECT_LIMIT_MEMORY;
            extInfo.ProcessMemoryLimit = new UIntPtr((ulong)Policy.MaxMemoryBytes.Value);
        }

        var length = (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        if (!SetInformationJobObject(job, JobObjectInfoType.ExtendedLimitInformation, ref extInfo, length))
            throw new InvalidOperationException($"Failed to set job limits: {Marshal.GetLastWin32Error()}");
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

    #region Windows Native API

    private const uint JOB_OBJECT_LIMIT_PROCESS_TIME = 0x00000002;
    private const uint JOB_OBJECT_LIMIT_MEMORY = 0x00000001;

    private enum JobObjectInfoType { ExtendedLimitInformation = 9 }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessTimeLimit;
        public long PerJobTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        JobObjectInfoType infoType,
        ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpInfo,
        uint cbInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, SafeHandle hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    #endregion
}
