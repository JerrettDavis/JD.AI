using System.ComponentModel;
using System.Text.Json;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Tracing;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Native exec/process tool surface for background session lifecycle management.
/// </summary>
[ToolPlugin("runtime", RequiresInjection = true)]
public sealed class ExecProcessTools
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Indented;

    private readonly ProcessSessionManager _manager;

    public ExecProcessTools(ProcessSessionManager manager)
    {
        _manager = manager;
    }

    [KernelFunction("exec")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("Execute a command with optional background mode, PTY emulation flag, timeout, and yield window. Returns a process session id for polling/logging.")]
    public async Task<string> ExecAsync(
        [Description("Command to execute")] string command,
        [Description("Working directory (optional)")] string? cwd = null,
        [Description("Yield duration before returning in milliseconds (default 250)")] int yieldMs = 250,
        [Description("Run in background and return immediately with a process session id")] bool background = false,
        [Description("Hard timeout in milliseconds (default 60000, set 0 to disable)")] int timeoutMs = 60_000,
        [Description("PTY compatibility mode flag")] bool pty = false,
        [Description("Execution host target. Only 'local' is currently supported")] string host = "local",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            return SerializeError("Command cannot be empty.");

        if (!string.Equals(host, "local", StringComparison.OrdinalIgnoreCase))
            return SerializeError($"Unsupported host target '{host}'. Only 'local' is allowed.");

        if (yieldMs < 0)
            return SerializeError("yieldMs must be greater than or equal to 0.");

        if (timeoutMs < 0)
            return SerializeError("timeoutMs must be greater than or equal to 0.");

        var request = new ProcessExecRequest(
            Command: command,
            WorkingDirectory: cwd,
            YieldMs: yieldMs,
            Background: background,
            TimeoutMs: timeoutMs,
            Pty: pty,
            Host: host);

        var scope = ResolveScopeKey();
        var snapshot = await _manager.ExecAsync(scope, request, ct).ConfigureAwait(false);
        var logs = _manager.GetLogs(scope, snapshot.SessionId, maxChars: 3_000);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "exec",
            scope,
            session = snapshot,
            stdout = logs?.Stdout ?? string.Empty,
            stderr = logs?.Stderr ?? string.Empty,
        }, JsonOptions);
    }

    [KernelFunction("process")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [Description("Manage process sessions. Actions: list, poll, log, write, kill, clear, remove.")]
    public async Task<string> ProcessAsync(
        [Description("Action: list, poll, log, write, kill, clear, remove")] string action,
        [Description("Process session id for actions that target a single session")] string? id = null,
        [Description("Optional stdin payload for write action")] string? input = null,
        [Description("Yield wait for poll action in milliseconds")] int yieldMs = 0,
        [Description("Maximum log characters to return (default 4000)")] int maxChars = 4_000,
        [Description("Force flag used by clear/remove to include running sessions")] bool force = false,
        CancellationToken ct = default)
    {
        var normalized = (action ?? string.Empty).Trim().ToLowerInvariant();
        var scope = ResolveScopeKey();

        return normalized switch
        {
            "list" => SerializeList(scope),
            "poll" => await SerializePollAsync(scope, id, yieldMs, ct).ConfigureAwait(false),
            "log" => SerializeLog(scope, id, maxChars),
            "write" => SerializeWrite(scope, id, input),
            "kill" => SerializeKill(scope, id),
            "clear" => SerializeClear(scope, force),
            "remove" => SerializeRemove(scope, id, force),
            _ => SerializeError("Unsupported process action. Valid: list, poll, log, write, kill, clear, remove."),
        };
    }

    private string SerializeList(string scope)
    {
        var sessions = _manager.List(scope, includeCompleted: true);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "list",
            scope,
            count = sessions.Count,
            sessions,
        }, JsonOptions);
    }

    private async Task<string> SerializePollAsync(
        string scope,
        string? id,
        int yieldMs,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return SerializeError("Action 'poll' requires id.");
        if (yieldMs < 0)
            return SerializeError("yieldMs must be greater than or equal to 0.");

        var session = await _manager.PollAsync(scope, id, yieldMs, ct).ConfigureAwait(false);
        if (session is null)
            return SerializeError($"Unknown process session '{id}'.");

        var logs = _manager.GetLogs(scope, id, maxChars: 2_000);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "poll",
            scope,
            session,
            stdout = logs?.Stdout ?? string.Empty,
            stderr = logs?.Stderr ?? string.Empty,
        }, JsonOptions);
    }

    private string SerializeLog(string scope, string? id, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(id))
            return SerializeError("Action 'log' requires id.");
        if (maxChars <= 0)
            return SerializeError("maxChars must be greater than 0.");

        var logs = _manager.GetLogs(scope, id, maxChars);
        if (logs is null)
            return SerializeError($"Unknown process session '{id}'.");

        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "log",
            scope,
            logs,
        }, JsonOptions);
    }

    private string SerializeWrite(string scope, string? id, string? input)
    {
        if (string.IsNullOrWhiteSpace(id))
            return SerializeError("Action 'write' requires id.");
        if (input is null)
            return SerializeError("Action 'write' requires input.");

        var success = _manager.TryWriteInput(scope, id, input, out var snapshot, out var error);
        if (!success)
            return SerializeError(error ?? "Failed to write to process.");

        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "write",
            scope,
            session = snapshot,
        }, JsonOptions);
    }

    private string SerializeKill(string scope, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return SerializeError("Action 'kill' requires id.");

        var success = _manager.TryKill(scope, id, out var snapshot, out var error);
        if (!success)
            return SerializeError(error ?? "Failed to kill process.");

        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "kill",
            scope,
            session = snapshot,
        }, JsonOptions);
    }

    private string SerializeClear(string scope, bool force)
    {
        var removed = _manager.Clear(scope, includeRunning: force);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "clear",
            scope,
            removed,
            force,
        }, JsonOptions);
    }

    private string SerializeRemove(string scope, string? id, bool force)
    {
        if (string.IsNullOrWhiteSpace(id))
            return SerializeError("Action 'remove' requires id.");

        var success = _manager.TryRemove(scope, id, force, out var error);
        if (!success)
            return SerializeError(error ?? "Failed to remove process.");

        return JsonSerializer.Serialize(new
        {
            ok = true,
            action = "remove",
            scope,
            id,
            force,
        }, JsonOptions);
    }

    private static string SerializeError(string message) =>
        JsonSerializer.Serialize(new { ok = false, error = message }, JsonOptions);

    internal static string ResolveScopeKey()
    {
        var ctx = TraceContext.CurrentContext;
        var session = string.IsNullOrWhiteSpace(ctx.SessionId) ? "session:default" : ctx.SessionId!;
        var agent = string.IsNullOrWhiteSpace(ctx.ParentAgentId) ? "agent:root" : ctx.ParentAgentId!;
        return $"{session}::{agent}";
    }
}

