namespace JD.AI.Sandbox.Abstractions;

/// <summary>
/// Contract for a process isolation layer that enforces capability restrictions
/// on a child process based on a <see cref="SandboxPolicy"/>.
/// </summary>
public interface ISandbox
{
    /// <summary>The policy this sandbox enforces.</summary>
    SandboxPolicy Policy { get; }

    /// <summary>Which platform this sandbox targets.</summary>
    SandboxPlatform Platform { get; }

    /// <summary>
    /// Starts a new sandboxed process using the configured policy.
    /// </summary>
    /// <param name="executablePath">Path to the executable to run.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A handle to the running sandboxed process.</returns>
    Task<SandboxedProcess> StartAsync(
        string executablePath,
        string arguments = "",
        CancellationToken ct = default);

    /// <summary>
    /// Runs the sandboxed process to completion and returns the result.
    /// </summary>
    Task<SandboxExecutionResult> RunAsync(
        string executablePath,
        string arguments = "",
        CancellationToken ct = default);
}

/// <summary>
/// Target platform for sandbox enforcement.
/// </summary>
public enum SandboxPlatform
{
    /// <summary>Linux with Landlock LSM + seccomp-bpf.</summary>
    Linux,

    /// <summary>Windows with Job Objects + Restricted Tokens.</summary>
    Windows,

    /// <summary>Cross-platform using simulated isolation (no real OS enforcement).</summary>
    None,
}
