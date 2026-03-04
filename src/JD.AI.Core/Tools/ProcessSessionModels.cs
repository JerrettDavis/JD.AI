namespace JD.AI.Core.Tools;

/// <summary>
/// State of an exec/process session.
/// </summary>
public enum ProcessSessionStatus
{
    Running,
    Completed,
    Failed,
    TimedOut,
    Killed,
    Orphaned,
}

/// <summary>
/// Input for <c>exec</c>.
/// </summary>
public sealed record ProcessExecRequest(
    string Command,
    string? WorkingDirectory = null,
    int YieldMs = 250,
    bool Background = false,
    int TimeoutMs = 60_000,
    bool Pty = false,
    string Host = "local");

/// <summary>
/// Snapshot of a managed process session.
/// </summary>
public sealed record ProcessSessionSnapshot(
    string SessionId,
    string ScopeKey,
    string Command,
    string? WorkingDirectory,
    bool Pty,
    string Host,
    ProcessSessionStatus Status,
    int? ProcessId,
    int? ExitCode,
    string? FailureReason,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int StdoutChars,
    int StderrChars);

/// <summary>
/// Log payload for a managed process session.
/// </summary>
public sealed record ProcessSessionLogs(
    string SessionId,
    string Stdout,
    string Stderr,
    int StdoutChars,
    int StderrChars);

