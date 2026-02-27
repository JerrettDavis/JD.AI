namespace JD.AI.Tui.Tools.Sandbox;

/// <summary>
/// Abstraction for executing commands with varying levels of isolation.
/// </summary>
public interface ISandbox
{
    /// <summary>The sandbox mode name.</summary>
    string ModeName { get; }

    /// <summary>Execute a command within the sandbox.</summary>
    Task<SandboxResult> ExecuteAsync(
        string command,
        string workingDirectory,
        int timeoutSeconds = 60,
        CancellationToken ct = default);
}

/// <summary>Result of a sandboxed command execution.</summary>
public sealed record SandboxResult(
    int ExitCode,
    string Output,
    string Error,
    bool TimedOut);
