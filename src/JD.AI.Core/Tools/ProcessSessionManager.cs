using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Config;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Core.Tools;

/// <summary>
/// Manages lifecycle of background and foreground process sessions used by
/// the <c>exec</c>/<c>process</c> tool surface.
/// </summary>
public sealed class ProcessSessionManager
{
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Indented;

    private readonly ConcurrentDictionary<string, ScopeState> _scopes =
        new(StringComparer.Ordinal);

    // Tracks every ExitMonitor task ever started so WaitForIdleAsync can
    // await them even after the corresponding session has been removed via Clear().
    // The queue is drained on each WaitForIdleAsync call so completed Task
    // objects are not retained indefinitely.
    private readonly ConcurrentQueue<Task> _allExitMonitors = new();

    private readonly string _metadataRoot;
    private readonly TimeSpan _completedRetention;
    private readonly int _maxLogCharsPerStream;

    public ProcessSessionManager(
        string? metadataRoot = null,
        TimeSpan? completedRetention = null,
        int maxLogCharsPerStream = 200_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLogCharsPerStream);

        _metadataRoot = metadataRoot ?? Path.Combine(DataDirectories.Root, "process-sessions");
        _completedRetention = completedRetention ?? TimeSpan.FromHours(12);
        _maxLogCharsPerStream = maxLogCharsPerStream;

        Directory.CreateDirectory(_metadataRoot);
        LoadPersistedMetadata();
    }

    public async Task<ProcessSessionSnapshot> ExecAsync(
        string scopeKey,
        ProcessExecRequest request,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Command))
            throw new ArgumentException("Command cannot be empty.", nameof(request));

        PruneCompleted(scopeKey);

        var scope = _scopes.GetOrAdd(scopeKey, _ => new ScopeState());
        var sessionId = scope.NextSessionId();
        var metadataPath = GetMetadataPath(scopeKey, sessionId);
        var record = new ProcessRecord(scopeKey, sessionId, request, metadataPath);
        scope.Sessions[sessionId] = record;

        try
        {
            var process = CreateProcess(request);
            record.Process = process;
            process.Start();
            record.ProcessId = process.Id;
            Persist(record);

            record.StdoutPump = PumpOutputAsync(record, process.StandardOutput, isError: false);
            record.StderrPump = PumpOutputAsync(record, process.StandardError, isError: true);
            record.ExitMonitor = MonitorExitAsync(record);
            _allExitMonitors.Enqueue(record.ExitMonitor);

            if (request.TimeoutMs > 0)
            {
                record.TimeoutMonitor = EnforceTimeoutAsync(record, request.TimeoutMs);
            }
        }
        catch (Exception ex)
        {
            lock (record.Sync)
            {
                record.Status = ProcessSessionStatus.Failed;
                record.FailureReason = $"Failed to start process: {ex.Message}";
                record.EndedAtUtc = DateTimeOffset.UtcNow;
            }

            record.Completion.TrySetResult(true);
            Persist(record);
            return Snapshot(record);
        }

        if (request.Background)
        {
            if (request.YieldMs > 0)
            {
                await WaitForCompletionOrDelayAsync(record, request.YieldMs, ct).ConfigureAwait(false);
            }

            return Snapshot(record);
        }

        await record.Completion.Task.WaitAsync(ct).ConfigureAwait(false);
        return Snapshot(record);
    }

    public IReadOnlyList<ProcessSessionSnapshot> List(string scopeKey, bool includeCompleted = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeKey);
        PruneCompleted(scopeKey);

        if (!_scopes.TryGetValue(scopeKey, out var scope))
            return [];

        return scope.Sessions.Values
            .Select(Snapshot)
            .Where(s => includeCompleted || s.Status == ProcessSessionStatus.Running)
            .OrderByDescending(s => s.StartedAtUtc)
            .ToArray();
    }

    public async Task<ProcessSessionSnapshot?> PollAsync(
        string scopeKey,
        string sessionId,
        int yieldMs = 0,
        CancellationToken ct = default)
    {
        if (!TryGetRecord(scopeKey, sessionId, out var record))
            return null;

        if (yieldMs > 0 && IsRunning(record))
        {
            await WaitForCompletionOrDelayAsync(record, yieldMs, ct).ConfigureAwait(false);
        }

        return Snapshot(record);
    }

    public ProcessSessionLogs? GetLogs(string scopeKey, string sessionId, int maxChars = 4_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);

        if (!TryGetRecord(scopeKey, sessionId, out var record))
            return null;

        lock (record.Sync)
        {
            return new ProcessSessionLogs(
                sessionId,
                Tail(record.Stdout, maxChars),
                Tail(record.Stderr, maxChars),
                record.Stdout.Length,
                record.Stderr.Length);
        }
    }

    public bool TryWriteInput(
        string scopeKey,
        string sessionId,
        string input,
        out ProcessSessionSnapshot? snapshot,
        out string? error)
    {
        snapshot = null;
        error = null;

        if (!TryGetRecord(scopeKey, sessionId, out var record))
        {
            error = $"Unknown process session '{sessionId}'.";
            return false;
        }

        if (!IsRunning(record))
        {
            snapshot = Snapshot(record);
            error = $"Process session '{sessionId}' is not running.";
            return false;
        }

        var standardInput = record.Process!.StandardInput;

        standardInput.Write(input);
        if (!input.EndsWith('\n'))
        {
            standardInput.WriteLine();
        }

        standardInput.Flush();
        snapshot = Snapshot(record);
        return true;
    }

    public bool TryKill(
        string scopeKey,
        string sessionId,
        out ProcessSessionSnapshot? snapshot,
        out string? error)
    {
        snapshot = null;
        error = null;

        if (!TryGetRecord(scopeKey, sessionId, out var record))
        {
            error = $"Unknown process session '{sessionId}'.";
            return false;
        }

        if (!IsRunning(record))
        {
            snapshot = Snapshot(record);
            return true;
        }

        SetFinalState(record, ProcessSessionStatus.Killed, null, "Killed by user.");
        if (record.Process is { HasExited: false } process)
        {
            process.Kill(entireProcessTree: true);
        }

        Persist(record);
        record.Completion.TrySetResult(true);
        snapshot = Snapshot(record);
        return true;
    }

    public int Clear(string scopeKey, bool includeRunning = false)
    {
        if (!_scopes.TryGetValue(scopeKey, out var scope))
            return 0;

        var removed = 0;
        foreach (var pair in scope.Sessions.ToArray())
        {
            var record = pair.Value;
            if (IsRunning(record) && !includeRunning)
                continue;

            if (IsRunning(record) && includeRunning)
            {
                TryKill(scopeKey, pair.Key, out _, out _);
            }

            if (scope.Sessions.TryRemove(pair.Key, out var removedRecord))
            {
                DeleteMetadata(removedRecord);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Waits for all background session tasks (stdout/stderr pumps and exit
    /// monitors) to complete.  Call this after <see cref="Clear"/> to ensure
    /// no orphaned I/O threads remain before the caller exits — for example,
    /// at the end of a test fixture tear-down so that CLR profilers (e.g.
    /// coverage collectors) can finalize cleanly.
    /// </summary>
    public async Task WaitForIdleAsync(CancellationToken ct = default)
    {
        // Drain the queue, collecting only tasks that haven't completed yet.
        // Draining keeps the queue small: completed Task objects are released
        // and become eligible for GC.
        var pending = new List<Task>();
        while (_allExitMonitors.TryDequeue(out var t))
        {
            if (!t.IsCompleted)
                pending.Add(t);
        }

        if (pending.Count == 0)
            return;

        try
        {
            await Task.WhenAll(pending).WaitAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch
        {
            // Background tasks may fault or be cancelled — that is fine;
            // we only need them to reach a terminal state.
        }
#pragma warning restore CA1031
    }

    public bool TryRemove(
        string scopeKey,
        string sessionId,
        bool force,
        out string? error)
    {
        error = null;
        if (!_scopes.TryGetValue(scopeKey, out var scope)
            || !scope.Sessions.TryGetValue(sessionId, out var record))
        {
            error = $"Unknown process session '{sessionId}'.";
            return false;
        }

        if (IsRunning(record))
        {
            if (!force)
            {
                error = $"Process session '{sessionId}' is running. Use force=true to remove.";
                return false;
            }

            TryKill(scopeKey, sessionId, out _, out _);
        }

        scope.Sessions.TryRemove(sessionId, out _);
        DeleteMetadata(record);
        return true;
    }

    private static async Task WaitForCompletionOrDelayAsync(
        ProcessRecord record,
        int yieldMs,
        CancellationToken ct)
    {
        var delayTask = Task.Delay(yieldMs, ct);
        await Task.WhenAny(record.Completion.Task, delayTask).ConfigureAwait(false);
    }

    private ProcessStartInfo CreateProcessStartInfo(ProcessExecRequest request)
    {
        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = isWindows
                ? $"/c {request.Command}"
                : $"-c \"{request.Command.Replace("\"", "\\\"")}\"",
            WorkingDirectory = request.WorkingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = !request.Pty,
        };

        if (request.Pty)
        {
            psi.Environment["TERM"] = "xterm-256color";
        }

        return psi;
    }

    private Process CreateProcess(ProcessExecRequest request)
    {
        var psi = CreateProcessStartInfo(request);
        return new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true,
        };
    }

    private async Task PumpOutputAsync(
        ProcessRecord record,
        StreamReader reader,
        bool isError)
    {
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            if (read <= 0)
                break;

            var chunk = new string(buffer, 0, read);
            lock (record.Sync)
            {
                var target = isError ? record.Stderr : record.Stdout;
                target.Append(chunk);
                if (target.Length > _maxLogCharsPerStream)
                {
                    target.Remove(0, target.Length - _maxLogCharsPerStream);
                }
            }
        }
    }

    private async Task MonitorExitAsync(ProcessRecord record)
    {
        await record.Process!.WaitForExitAsync().ConfigureAwait(false);
        if (record.StdoutPump is not null)
            await record.StdoutPump.ConfigureAwait(false);
        if (record.StderrPump is not null)
            await record.StderrPump.ConfigureAwait(false);

        lock (record.Sync)
        {
            if (record.Status == ProcessSessionStatus.Running)
            {
                record.Status = record.Process.ExitCode == 0
                    ? ProcessSessionStatus.Completed
                    : ProcessSessionStatus.Failed;
            }

            record.ExitCode ??= record.Process.ExitCode;
            record.EndedAtUtc ??= DateTimeOffset.UtcNow;
        }

        record.Completion.TrySetResult(true);
        Persist(record);
    }

    private async Task EnforceTimeoutAsync(ProcessRecord record, int timeoutMs)
    {
        await Task.Delay(timeoutMs).ConfigureAwait(false);

        if (!IsRunning(record))
            return;

        SetFinalState(record, ProcessSessionStatus.TimedOut, null, $"Execution timed out after {timeoutMs}ms.");
        if (record.Process is { HasExited: false } process)
        {
            process.Kill(entireProcessTree: true);
        }

        record.Completion.TrySetResult(true);
        Persist(record);
    }

    private void SetFinalState(
        ProcessRecord record,
        ProcessSessionStatus status,
        int? exitCode,
        string? failureReason)
    {
        lock (record.Sync)
        {
            record.Status = status;
            record.ExitCode = exitCode ?? record.ExitCode;
            record.FailureReason = failureReason;
            record.EndedAtUtc ??= DateTimeOffset.UtcNow;
        }
    }

    private static bool IsRunning(ProcessRecord record)
    {
        lock (record.Sync)
        {
            return record.Status == ProcessSessionStatus.Running;
        }
    }

    private bool TryGetRecord(string scopeKey, string sessionId, out ProcessRecord record)
    {
        if (_scopes.TryGetValue(scopeKey, out var scope)
            && scope.Sessions.TryGetValue(sessionId, out var found))
        {
            record = found;
            return true;
        }

        record = null!;
        return false;
    }

    private ProcessSessionSnapshot Snapshot(ProcessRecord record)
    {
        lock (record.Sync)
        {
            return new ProcessSessionSnapshot(
                SessionId: record.SessionId,
                ScopeKey: record.ScopeKey,
                Command: record.Command,
                WorkingDirectory: record.WorkingDirectory,
                Pty: record.Pty,
                Host: record.Host,
                Status: record.Status,
                ProcessId: record.ProcessId,
                ExitCode: record.ExitCode,
                FailureReason: record.FailureReason,
                StartedAtUtc: record.StartedAtUtc,
                EndedAtUtc: record.EndedAtUtc,
                StdoutChars: record.Stdout.Length,
                StderrChars: record.Stderr.Length);
        }
    }

    private void PruneCompleted(string scopeKey)
    {
        if (!_scopes.TryGetValue(scopeKey, out var scope))
            return;

        var threshold = DateTimeOffset.UtcNow - _completedRetention;
        foreach (var pair in scope.Sessions.ToArray())
        {
            var record = pair.Value;
            bool remove;
            lock (record.Sync)
            {
                remove = record.Status is not ProcessSessionStatus.Running
                    && record.EndedAtUtc is { } ended
                    && ended < threshold;
            }

            if (!remove)
                continue;

            if (scope.Sessions.TryRemove(pair.Key, out var removed))
            {
                DeleteMetadata(removed);
            }
        }
    }

    private string GetMetadataPath(string scopeKey, string sessionId)
    {
        var key = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(scopeKey)))
            .ToLowerInvariant();
        return Path.Combine(_metadataRoot, $"{key}-{sessionId}.json");
    }

    private void Persist(ProcessRecord record)
    {
        var snapshot = Snapshot(record);
        var payload = new PersistedProcessSession
        {
            SessionId = snapshot.SessionId,
            ScopeKey = snapshot.ScopeKey,
            Command = snapshot.Command,
            WorkingDirectory = snapshot.WorkingDirectory,
            Pty = snapshot.Pty,
            Host = snapshot.Host,
            Status = snapshot.Status,
            ProcessId = snapshot.ProcessId,
            ExitCode = snapshot.ExitCode,
            FailureReason = snapshot.FailureReason,
            StartedAtUtc = snapshot.StartedAtUtc,
            EndedAtUtc = snapshot.EndedAtUtc,
            StdoutChars = snapshot.StdoutChars,
            StderrChars = snapshot.StderrChars,
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        try
        {
            File.WriteAllText(record.MetadataPath, json);
        }
#pragma warning disable CA1031
        catch
        {
            // Best-effort persistence — directory may have been removed or the
            // metadata root is unavailable. This is non-fatal.
        }
#pragma warning restore CA1031
    }

    private void DeleteMetadata(ProcessRecord record)
    {
        try
        {
            if (File.Exists(record.MetadataPath))
                File.Delete(record.MetadataPath);
        }
#pragma warning disable CA1031
        catch
        {
            // Best effort cleanup.
        }
#pragma warning restore CA1031
    }

    private void LoadPersistedMetadata()
    {
        foreach (var file in Directory.EnumerateFiles(_metadataRoot, "*.json"))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<PersistedProcessSession>(File.ReadAllText(file), JsonOptions);
                if (payload is null
                    || string.IsNullOrWhiteSpace(payload.ScopeKey)
                    || string.IsNullOrWhiteSpace(payload.SessionId)
                    || string.IsNullOrWhiteSpace(payload.Command))
                {
                    continue;
                }

                var scope = _scopes.GetOrAdd(payload.ScopeKey, _ => new ScopeState());
                scope.TrackSequence(payload.SessionId);

                var status = payload.Status == ProcessSessionStatus.Running
                    ? ProcessSessionStatus.Orphaned
                    : payload.Status;
                var failure = payload.Status == ProcessSessionStatus.Running
                    ? "Recovered orphaned process metadata from previous JD.AI run."
                    : payload.FailureReason;

                var request = new ProcessExecRequest(
                    payload.Command,
                    payload.WorkingDirectory,
                    YieldMs: 0,
                    Background: true,
                    TimeoutMs: 0,
                    Pty: payload.Pty,
                    Host: payload.Host ?? "local");

                var record = new ProcessRecord(payload.ScopeKey, payload.SessionId, request, file)
                {
                    ProcessId = payload.ProcessId,
                    ExitCode = payload.ExitCode,
                };

                lock (record.Sync)
                {
                    record.Status = status;
                    record.FailureReason = failure;
                    record.StartedAtUtc = payload.StartedAtUtc;
                    record.EndedAtUtc = payload.EndedAtUtc ?? payload.StartedAtUtc;
                    if (payload.StdoutChars > 0)
                    {
                        record.Stdout.Append($"[persisted] stdout chars: {payload.StdoutChars}");
                    }

                    if (payload.StderrChars > 0)
                    {
                        record.Stderr.Append($"[persisted] stderr chars: {payload.StderrChars}");
                    }
                }

                record.Completion.TrySetResult(true);
                scope.Sessions[payload.SessionId] = record;
            }
#pragma warning disable CA1031
            catch
            {
                // Ignore malformed persisted entries.
            }
#pragma warning restore CA1031
        }
    }

    private static string Tail(StringBuilder value, int maxChars)
    {
        if (value.Length <= maxChars)
            return value.ToString();

        return value.ToString(value.Length - maxChars, maxChars);
    }

    private sealed class ScopeState
    {
        private long _sequence;

        public ConcurrentDictionary<string, ProcessRecord> Sessions { get; } =
            new(StringComparer.Ordinal);

        public string NextSessionId()
        {
            var next = Interlocked.Increment(ref _sequence);
            return $"proc-{next:D6}";
        }

        public void TrackSequence(string sessionId)
        {
            if (!sessionId.StartsWith("proc-", StringComparison.OrdinalIgnoreCase))
                return;

            var raw = sessionId["proc-".Length..];
            if (!long.TryParse(raw, out var parsed) || parsed <= 0)
                return;

            while (true)
            {
                var current = Volatile.Read(ref _sequence);
                if (parsed <= current)
                    return;

                if (Interlocked.CompareExchange(ref _sequence, parsed, current) == current)
                    return;
            }
        }
    }

    private sealed class ProcessRecord
    {
        public ProcessRecord(
            string scopeKey,
            string sessionId,
            ProcessExecRequest request,
            string metadataPath)
        {
            ScopeKey = scopeKey;
            SessionId = sessionId;
            Command = request.Command;
            WorkingDirectory = request.WorkingDirectory;
            Pty = request.Pty;
            Host = request.Host;
            MetadataPath = metadataPath;
        }

        public object Sync { get; } = new();
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public StringBuilder Stdout { get; } = new();
        public StringBuilder Stderr { get; } = new();

        public string ScopeKey { get; }
        public string SessionId { get; }
        public string Command { get; }
        public string? WorkingDirectory { get; }
        public bool Pty { get; }
        public string Host { get; }
        public string MetadataPath { get; }

        public Process? Process { get; set; }
        public int? ProcessId { get; set; }
        public int? ExitCode { get; set; }
        public ProcessSessionStatus Status { get; set; } = ProcessSessionStatus.Running;
        public string? FailureReason { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? EndedAtUtc { get; set; }

        public Task? StdoutPump { get; set; }
        public Task? StderrPump { get; set; }
        public Task? ExitMonitor { get; set; }
        public Task? TimeoutMonitor { get; set; }
    }

    private sealed class PersistedProcessSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string ScopeKey { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string? WorkingDirectory { get; set; }
        public bool Pty { get; set; }
        public string? Host { get; set; }
        public ProcessSessionStatus Status { get; set; }
        public int? ProcessId { get; set; }
        public int? ExitCode { get; set; }
        public string? FailureReason { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset? EndedAtUtc { get; set; }
        public int StdoutChars { get; set; }
        public int StderrChars { get; set; }
    }
}
