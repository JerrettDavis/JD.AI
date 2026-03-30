using JD.AI.Sandbox.Abstractions;

namespace JD.AI.Sandbox.Policies;

/// <summary>
/// Common pre-built policy configurations for common isolation scenarios.
/// </summary>
public static class SandboxPolicies
{
    /// <summary>
    /// Planner policy: can make outbound API calls, cannot touch the filesystem or spawn processes.
    /// Ideal for the "thinking" phase that decides what to do without seeing real data.
    /// </summary>
    public static SandboxPolicy Planner(string? allowedApiHost = null) =>
        new()
        {
            Name = "Planner",
            AllowNetwork = true,
            AllowRead = false,
            AllowWrite = false,
            AllowProcessSpawn = false,
        };

    /// <summary>
    /// Executor policy: can read/write allowed filesystem paths, cannot make outbound network calls.
    /// Ideal for the "acting" phase that modifies data but must not exfiltrate it.
    /// </summary>
    public static SandboxPolicy Executor(params string[] allowedPaths) =>
        new()
        {
            Name = "Executor",
            AllowNetwork = false,
            AllowRead = true,
            AllowWrite = true,
            AllowedPaths = allowedPaths,
            AllowProcessSpawn = false,
        };

    /// <summary>
    /// Read-only executor policy: can read allowed filesystem paths, cannot write or make network calls.
    /// Ideal for audit/review operations.
    /// </summary>
    public static SandboxPolicy ReadOnly(params string[] allowedPaths) =>
        new()
        {
            Name = "ReadOnly",
            AllowNetwork = false,
            AllowRead = true,
            AllowWrite = false,
            AllowedPaths = allowedPaths,
            AllowProcessSpawn = false,
        };

    /// <summary>
    /// Fully locked down policy: no network, no filesystem, no process spawn.
    /// Useful as a deny-by-default baseline.
    /// </summary>
    public static SandboxPolicy LockedDown() =>
        new()
        {
            Name = "LockedDown",
            AllowNetwork = false,
            AllowRead = false,
            AllowWrite = false,
            AllowProcessSpawn = false,
        };

    /// <summary>
    /// Creates a policy with resource limits (CPU time and memory).
    /// </summary>
    public static SandboxPolicy WithLimits(this SandboxPolicy policy, int? maxCpuTimeMs = null, long? maxMemoryBytes = null) =>
        new()
        {
            Name = policy.Name,
            AllowNetwork = policy.AllowNetwork,
            AllowRead = policy.AllowRead,
            AllowWrite = policy.AllowWrite,
            AllowedPaths = policy.AllowedPaths,
            DeniedPaths = policy.DeniedPaths,
            AllowProcessSpawn = policy.AllowProcessSpawn,
            MaxCpuTimeMs = maxCpuTimeMs ?? policy.MaxCpuTimeMs,
            MaxMemoryBytes = maxMemoryBytes ?? policy.MaxMemoryBytes,
            EnvironmentVariables = policy.EnvironmentVariables,
            WorkingDirectory = policy.WorkingDirectory,
        };
}
