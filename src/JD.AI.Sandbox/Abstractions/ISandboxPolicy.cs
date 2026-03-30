using System.Diagnostics;

namespace JD.AI.Sandbox.Abstractions;

/// <summary>
/// Defines the capability profile for a sandboxed process.
/// Each policy describes what resources and operations are allowed or denied.
/// </summary>
public sealed class SandboxPolicy
{
    /// <summary>Human-readable name for this policy (e.g., "PlannerPolicy", "ExecutorPolicy").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Whether the sandboxed process can make outbound network connections.</summary>
    public bool AllowNetwork { get; init; } = true;

    /// <summary>Whether the sandboxed process can read from the filesystem.</summary>
    public bool AllowRead { get; init; } = true;

    /// <summary>Whether the sandboxed process can write to the filesystem.</summary>
    public bool AllowWrite { get; init; } = true;

    /// <summary>
    /// Explicitly allowed filesystem paths (if non-empty, all other paths are denied for read/write).
    /// Supports glob patterns. Only meaningful when <see cref="AllowRead"/> or <see cref="AllowWrite"/> is true.
    /// </summary>
    public IReadOnlyList<string> AllowedPaths { get; init; } = [];

    /// <summary>
    /// Explicitly denied filesystem paths. Takes precedence over <see cref="AllowedPaths"/>.
    /// Supports glob patterns.
    /// </summary>
    public IReadOnlyList<string> DeniedPaths { get; init; } = [];

    /// <summary>Whether the sandboxed process can spawn child processes.</summary>
    public bool AllowProcessSpawn { get; init; }

    /// <summary>Maximum CPU time allowed (in milliseconds) per execution. null = unlimited.</summary>
    public int? MaxCpuTimeMs { get; init; }

    /// <summary>Maximum memory allowed (in bytes) per execution. null = unlimited.</summary>
    public long? MaxMemoryBytes { get; init; }

    /// <summary>
    /// Environment variables that will be passed to the sandboxed process.
    /// Empty = inherit all from parent.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>();

    /// <summary>
    /// Working directory for the sandboxed process.
    /// null = inherit from parent.
    /// </summary>
    public string? WorkingDirectory { get; init; }
}

/// <summary>
/// Represents a started sandboxed process.
/// </summary>
public sealed class SandboxedProcess : IAsyncDisposable
{
    /// <summary>Process ID of the sandboxed process.</summary>
    public int ProcessId { get; }

    /// <summary>Standard input stream to the sandboxed process.</summary>
    public StreamWriter StandardInput => _standardInput;

    /// <summary>Standard output stream from the sandboxed process.</summary>
    public StreamReader StandardOutput => _standardOutput;

    /// <summary>Standard error stream from the sandboxed process.</summary>
    public StreamReader StandardError => _standardError;

    private readonly Process _process;
    private readonly StreamWriter _standardInput;
    private readonly StreamReader _standardOutput;
    private readonly StreamReader _standardError;
    private bool _disposed;

    internal SandboxedProcess(Process process, StreamWriter standardInput, StreamReader standardOutput, StreamReader standardError)
    {
        _process = process;
        _standardInput = standardInput;
        _standardOutput = standardOutput;
        _standardError = standardError;
        ProcessId = process.Id;
    }

    /// <summary>Waits for the process to exit.</summary>
    public async Task WaitForExitAsync(CancellationToken ct = default)
    {
        await _process.WaitForExitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Gets the process exit code.</summary>
    public int ExitCode => _process.ExitCode;

    /// <summary>Kills the sandboxed process and all its children.</summary>
    public void Kill()
    {
        try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _standardInput.Dispose();
        _standardOutput.Dispose();
        _standardError.Dispose();
        _process.Dispose();
        await ValueTask.CompletedTask;
    }
}

/// <summary>
/// Result of a sandboxed execution.
/// </summary>
public sealed class SandboxExecutionResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Elapsed { get; init; }
    public string? Error { get; init; }
}
