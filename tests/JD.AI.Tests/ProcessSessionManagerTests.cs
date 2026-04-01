using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class ProcessSessionManagerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly ProcessSessionManager _manager;

    public ProcessSessionManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-proc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _manager = new ProcessSessionManager(
            metadataRoot: _tempDir,
            completedRetention: TimeSpan.FromMinutes(30));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecAsync_BackgroundThenPoll_CompletesWithOutput()
    {
        const string Scope = "session-a::agent-a";
        var snapshot = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: SleepThenEcho("done-a", 0.25),
            Background: true,
            YieldMs: 25,
            TimeoutMs: 5_000));

        Assert.StartsWith("proc-", snapshot.SessionId, StringComparison.Ordinal);

        var polled = await _manager.PollAsync(Scope, snapshot.SessionId, yieldMs: 2_000);
        Assert.NotNull(polled);
        Assert.NotEqual(ProcessSessionStatus.Running, polled!.Status);

        var logs = _manager.GetLogs(Scope, snapshot.SessionId, maxChars: 2_000);
        Assert.NotNull(logs);
        Assert.Contains("done-a", logs!.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryWriteInput_WritesToStdin_AndAppearsInLogs()
    {
        const string Scope = "session-b::agent-b";
        var snapshot = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: ReadAndEchoCommand(),
            Background: true,
            YieldMs: 0,
            TimeoutMs: 5_000));

        var wrote = _manager.TryWriteInput(Scope, snapshot.SessionId, "hello-stdin", out _, out var error);
        Assert.True(wrote, error);

        var done = await _manager.PollAsync(Scope, snapshot.SessionId, yieldMs: 3_000);
        Assert.NotNull(done);
        Assert.NotEqual(ProcessSessionStatus.Running, done!.Status);

        var logs = _manager.GetLogs(Scope, snapshot.SessionId, maxChars: 2_000);
        Assert.NotNull(logs);
        Assert.Contains("hello-stdin", logs!.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KillAndRemove_RespectForceFlag()
    {
        const string Scope = "session-c::agent-c";
        var snapshot = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: SleepCommand(5),
            Background: true,
            YieldMs: 0,
            TimeoutMs: 20_000));

        var removedWithoutForce = _manager.TryRemove(Scope, snapshot.SessionId, force: false, out var removeError);
        Assert.False(removedWithoutForce);
        Assert.Contains("running", removeError, StringComparison.OrdinalIgnoreCase);

        var killed = _manager.TryKill(Scope, snapshot.SessionId, out var killedSnapshot, out var killError);
        Assert.True(killed, killError);
        Assert.NotNull(killedSnapshot);
        Assert.Equal(ProcessSessionStatus.Killed, killedSnapshot!.Status);

        var removed = _manager.TryRemove(Scope, snapshot.SessionId, force: false, out var finalError);
        Assert.True(removed, finalError);
        Assert.Empty(_manager.List(Scope));
    }

    [Fact]
    public async Task ScopeIsolation_ListsOnlyOwnSessions()
    {
        const string ScopeA = "session-d::agent-a";
        const string ScopeB = "session-d::agent-b";

        _ = await _manager.ExecAsync(ScopeA, new ProcessExecRequest("echo a", Background: true, YieldMs: 0, TimeoutMs: 2_000));
        _ = await _manager.ExecAsync(ScopeB, new ProcessExecRequest("echo b", Background: true, YieldMs: 0, TimeoutMs: 2_000));

        var aSessions = _manager.List(ScopeA);
        var bSessions = _manager.List(ScopeB);

        Assert.Single(aSessions);
        Assert.Single(bSessions);
        Assert.All(aSessions, s => Assert.Equal(ScopeA, s.ScopeKey));
        Assert.All(bSessions, s => Assert.Equal(ScopeB, s.ScopeKey));
    }

    [Fact]
    public async Task Clear_RemovesCompletedSessions()
    {
        const string Scope = "session-e::agent-a";
        var completed = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo clear-me",
            Background: false,
            TimeoutMs: 5_000));

        Assert.NotEqual(ProcessSessionStatus.Running, completed.Status);
        Assert.NotEmpty(_manager.List(Scope));

        var removed = _manager.Clear(Scope, includeRunning: false);
        Assert.Equal(1, removed);
        Assert.Empty(_manager.List(Scope));
    }

    [Fact]
    public async Task List_UnknownScope_ReturnsEmpty_And_FilterRunningWorks()
    {
        Assert.Empty(_manager.List("unknown::scope"));

        const string Scope = "session-h::agent-list";
        _ = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: SleepThenEcho("done-list", 0.2),
            Background: true,
            YieldMs: 0,
            TimeoutMs: 5_000));

        var runningOnly = _manager.List(Scope, includeCompleted: false);
        Assert.All(runningOnly, s => Assert.Equal(ProcessSessionStatus.Running, s.Status));
    }

    [Fact]
    public async Task UnknownIds_ReturnExpectedFalseOrNullResults()
    {
        const string Scope = "session-i::agent-unknown";
        Assert.Null(await _manager.PollAsync(Scope, "proc-999999", yieldMs: 0));
        Assert.Null(_manager.GetLogs(Scope, "proc-999999"));

        var writeOk = _manager.TryWriteInput(Scope, "proc-999999", "x", out var writeSnapshot, out var writeError);
        Assert.False(writeOk);
        Assert.Null(writeSnapshot);
        Assert.Contains("Unknown process session", writeError, StringComparison.OrdinalIgnoreCase);

        var killOk = _manager.TryKill(Scope, "proc-999999", out var killSnapshot, out var killError);
        Assert.False(killOk);
        Assert.Null(killSnapshot);
        Assert.Contains("Unknown process session", killError, StringComparison.OrdinalIgnoreCase);

        var removeOk = _manager.TryRemove(Scope, "proc-999999", force: false, out var removeError);
        Assert.False(removeOk);
        Assert.Contains("Unknown process session", removeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteToCompletedProcess_ReturnsNotRunning()
    {
        const string Scope = "session-j::agent-write";
        var done = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo complete",
            Background: false,
            TimeoutMs: 5_000));

        var wrote = _manager.TryWriteInput(Scope, done.SessionId, "late-input", out var snapshot, out var error);
        Assert.False(wrote);
        Assert.NotNull(snapshot);
        Assert.Contains("not running", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Kill_AfterCompletion_IsNoOpSuccess()
    {
        const string Scope = "session-l::agent-kill";
        var done = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo kill-completed",
            Background: false,
            TimeoutMs: 5_000));

        var killed = _manager.TryKill(Scope, done.SessionId, out var snapshot, out var error);
        Assert.True(killed, error);
        Assert.NotNull(snapshot);
        Assert.NotEqual(ProcessSessionStatus.Running, snapshot!.Status);
    }

    [Fact]
    public async Task Remove_ForceTrue_KillsRunningSession()
    {
        const string Scope = "session-m::agent-remove";
        var running = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: SleepCommand(5),
            Background: true,
            YieldMs: 0,
            TimeoutMs: 20_000));

        var removed = _manager.TryRemove(Scope, running.SessionId, force: true, out var error);
        Assert.True(removed, error);
        Assert.Empty(_manager.List(Scope));
    }

    [Fact]
    public async Task Timeout_TransitionsToTimedOut()
    {
        const string Scope = "session-n::agent-timeout";
        var timed = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: SleepCommand(5),
            Background: true,
            YieldMs: 0,
            TimeoutMs: 200));

        var polled = await _manager.PollAsync(Scope, timed.SessionId, yieldMs: 2_000);
        Assert.NotNull(polled);
        Assert.Equal(ProcessSessionStatus.TimedOut, polled!.Status);
    }

    [Fact]
    public async Task TimeoutMonitor_DoesNotOverrideAlreadyCompletedSession()
    {
        const string Scope = "session-u::agent-timeout";
        var session = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo quick",
            Background: true,
            YieldMs: 0,
            TimeoutMs: 1_000));

        await Task.Delay(1_200);

        var polled = await _manager.PollAsync(Scope, session.SessionId, yieldMs: 2_000);
        Assert.NotNull(polled);
        Assert.NotEqual(ProcessSessionStatus.TimedOut, polled!.Status);
    }

    [Fact]
    public async Task InvalidWorkingDirectory_ProducesFailedSnapshot()
    {
        const string Scope = "session-o::agent-start";
        var result = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo hi",
            WorkingDirectory: Path.Combine(_tempDir, "missing-dir"),
            Background: false,
            TimeoutMs: 5_000));

        Assert.Equal(ProcessSessionStatus.Failed, result.Status);
        Assert.Contains("Failed to start process", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ValidatesPositiveMaxLogChars()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessSessionManager(metadataRoot: _tempDir, maxLogCharsPerStream: 0));
    }

    [Fact]
    public async Task ExecAsync_EmptyCommand_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.ExecAsync("session-t::agent-validate", new ProcessExecRequest(" ")));
    }

    [Fact]
    public async Task PtyAndTailBehavior_AreCaptured()
    {
        const string Scope = "session-p::agent-pty";
        var session = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: LongEchoCommand(),
            Background: false,
            TimeoutMs: 5_000,
            Pty: true));

        Assert.True(session.Pty);

        var logs = _manager.GetLogs(Scope, session.SessionId, maxChars: 50);
        Assert.NotNull(logs);
        Assert.True(logs!.Stdout.Length <= 50);
    }

    [Fact]
    public async Task MaxLogBuffer_TruncatesStoredOutput()
    {
        var truncatedManager = new ProcessSessionManager(
            metadataRoot: Path.Combine(_tempDir, "truncate"),
            completedRetention: TimeSpan.FromMinutes(30),
            maxLogCharsPerStream: 20);

        const string Scope = "session-v::agent-truncate";
        var session = await truncatedManager.ExecAsync(Scope, new ProcessExecRequest(
            Command: LongEchoCommand(),
            Background: false,
            TimeoutMs: 5_000));

        var logs = truncatedManager.GetLogs(Scope, session.SessionId, maxChars: 100);
        Assert.NotNull(logs);
        Assert.True(logs!.StdoutChars <= 20);
    }

    [Fact]
    public async Task PruneCompleted_RemovesExpiredSessions()
    {
        var shortRetentionManager = new ProcessSessionManager(
            metadataRoot: Path.Combine(_tempDir, "prune"),
            completedRetention: TimeSpan.FromMilliseconds(1));

        const string Scope = "session-q::agent-prune";
        _ = await shortRetentionManager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo prune",
            Background: false,
            TimeoutMs: 5_000));

        await Task.Delay(10);
        _ = shortRetentionManager.List(Scope);
        Assert.Empty(shortRetentionManager.List(Scope));
    }

    [Fact]
    public void LoadPersistedMetadata_HandlesMalformedAndPartialFiles()
    {
        var root = Path.Combine(_tempDir, "persisted");
        Directory.CreateDirectory(root);

        File.WriteAllText(Path.Combine(root, "broken.json"), "{ not-json");
        File.WriteAllText(Path.Combine(root, "partial.json"), "{\"SessionId\":\"x\"}");
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(Path.Combine(root, "valid-running.json"), JsonSerializer.Serialize(new
        {
            SessionId = "proc-000007",
            ScopeKey = "session-r::agent-persist",
            Command = "echo x",
            WorkingDirectory = (string?)null,
            Pty = false,
            Host = "local",
            Status = ProcessSessionStatus.Running,
            ProcessId = (int?)null,
            ExitCode = (int?)null,
            FailureReason = (string?)null,
            StartedAtUtc = now,
            EndedAtUtc = now,
            StdoutChars = 12,
            StderrChars = 3,
        }));
        File.WriteAllText(Path.Combine(root, "invalid-id.json"), JsonSerializer.Serialize(new
        {
            SessionId = "bad-id",
            ScopeKey = "session-r::agent-persist",
            Command = "echo y",
            WorkingDirectory = (string?)null,
            Pty = false,
            Host = "local",
            Status = ProcessSessionStatus.Completed,
            ProcessId = (int?)null,
            ExitCode = 0,
            FailureReason = (string?)null,
            StartedAtUtc = now,
            EndedAtUtc = now,
            StdoutChars = 0,
            StderrChars = 0,
        }));
        File.WriteAllText(Path.Combine(root, "invalid-proc-id.json"), JsonSerializer.Serialize(new
        {
            SessionId = "proc-abc",
            ScopeKey = "session-r::agent-persist",
            Command = "echo z",
            WorkingDirectory = (string?)null,
            Pty = false,
            Host = "local",
            Status = ProcessSessionStatus.Completed,
            ProcessId = (int?)null,
            ExitCode = 0,
            FailureReason = (string?)null,
            StartedAtUtc = now,
            EndedAtUtc = now,
            StdoutChars = 0,
            StderrChars = 0,
        }));
        File.WriteAllText(Path.Combine(root, "duplicate-seq.json"), JsonSerializer.Serialize(new
        {
            SessionId = "proc-000007",
            ScopeKey = "session-r::agent-persist",
            Command = "echo duplicate",
            WorkingDirectory = (string?)null,
            Pty = false,
            Host = "local",
            Status = ProcessSessionStatus.Completed,
            ProcessId = (int?)null,
            ExitCode = 0,
            FailureReason = (string?)null,
            StartedAtUtc = now,
            EndedAtUtc = now,
            StdoutChars = 0,
            StderrChars = 0,
        }));

        var loaded = new ProcessSessionManager(
            metadataRoot: root,
            completedRetention: TimeSpan.FromDays(3650));
        var sessions = loaded.List("session-r::agent-persist");

        Assert.True(sessions.Count >= 3);
        // proc-000007 appears in both valid-running.json (Running) and duplicate-seq.json (Completed).
        // Load order determines which wins. If Running wins → marked Orphaned (no live process).
        // If Completed wins → stays Completed. Both are valid outcomes for duplicate handling.
        Assert.Contains(sessions, s =>
            string.Equals(s.SessionId, "proc-000007", StringComparison.Ordinal)
            && s.Status is ProcessSessionStatus.Orphaned or ProcessSessionStatus.Completed);

        // GetLogs returns a result for recovered/orphaned sessions even without log files
        var recovered = loaded.GetLogs("session-r::agent-persist", "proc-000007");
        Assert.NotNull(recovered);
    }

    [Fact]
    public async Task DeleteMetadata_HandlesLockedFile()
    {
        const string Scope = "session-s::agent-delete";
        var session = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: "echo done",
            Background: false,
            TimeoutMs: 5_000));

        var metadataPath = BuildMetadataPath(_tempDir, Scope, session.SessionId);
        using var lockStream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var removed = _manager.TryRemove(Scope, session.SessionId, force: false, out var error);
        Assert.True(removed, error);
    }

    [Fact]
    public async Task ConcurrentExec_GeneratesUniqueSessionIds()
    {
        const string Scope = "session-f::agent-concurrent";
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _manager.ExecAsync(Scope, new ProcessExecRequest(
                Command: SleepThenEcho($"task-{i}", 0.1),
                Background: true,
                YieldMs: 0,
                TimeoutMs: 5_000)))
            .ToArray();

        var sessions = await Task.WhenAll(tasks);
        var distinct = sessions.Select(s => s.SessionId).Distinct(StringComparer.Ordinal).Count();
        Assert.Equal(sessions.Length, distinct);
    }

    [Fact]
    public async Task PersistedRunningSession_IsRecoveredAsOrphaned()
    {
        const string Scope = "session-g::agent-recovery";
        var started = await _manager.ExecAsync(Scope, new ProcessExecRequest(
            Command: SleepCommand(5),
            Background: true,
            YieldMs: 0,
            TimeoutMs: 20_000));

        var recovered = new ProcessSessionManager(
            metadataRoot: _tempDir,
            completedRetention: TimeSpan.FromMinutes(30));

        var sessions = recovered.List(Scope);
        var session = sessions.Single(s => string.Equals(s.SessionId, started.SessionId, StringComparison.Ordinal));
        Assert.Equal(ProcessSessionStatus.Orphaned, session.Status);

        _manager.TryKill(Scope, started.SessionId, out _, out _);
        recovered.Clear(Scope, includeRunning: true);
    }

    [Fact]
    public void GetLogs_InvalidMaxChars_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _manager.GetLogs("x", "y", maxChars: 0));
    }

    public async Task DisposeAsync()
    {
        _manager.Clear("session-a::agent-a", includeRunning: true);
        _manager.Clear("session-b::agent-b", includeRunning: true);
        _manager.Clear("session-c::agent-c", includeRunning: true);
        _manager.Clear("session-d::agent-a", includeRunning: true);
        _manager.Clear("session-d::agent-b", includeRunning: true);
        _manager.Clear("session-e::agent-a", includeRunning: true);
        _manager.Clear("session-f::agent-concurrent", includeRunning: true);
        _manager.Clear("session-g::agent-recovery", includeRunning: true);
        _manager.Clear("session-h::agent-list", includeRunning: true);
        _manager.Clear("session-i::agent-unknown", includeRunning: true);
        _manager.Clear("session-j::agent-write", includeRunning: true);
        _manager.Clear("session-k::agent-write", includeRunning: true);
        _manager.Clear("session-l::agent-kill", includeRunning: true);
        _manager.Clear("session-m::agent-remove", includeRunning: true);
        _manager.Clear("session-n::agent-timeout", includeRunning: true);
        _manager.Clear("session-o::agent-start", includeRunning: true);
        _manager.Clear("session-p::agent-pty", includeRunning: true);
        _manager.Clear("session-q::agent-prune", includeRunning: true);
        _manager.Clear("session-r::agent-persist", includeRunning: true);
        _manager.Clear("session-s::agent-delete", includeRunning: true);
        _manager.Clear("session-t::agent-validate", includeRunning: true);
        _manager.Clear("session-u::agent-timeout", includeRunning: true);

        // After killing processes, wait for background I/O tasks (stdout/stderr
        // pumps and exit monitors) to drain.  This prevents dangling threads
        // from blocking coverage-data finalisation when the CLR profiler
        // (e.g. coverlet) tries to write coverage output after the test session.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _manager.WaitForIdleAsync(cts.Token);

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for test temp files.
        }
    }

    private static string SleepThenEcho(string marker, double seconds)
    {
        if (OperatingSystem.IsWindows())
        {
            var ms = (int)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero);
            return $"powershell -NoProfile -Command \"Start-Sleep -Milliseconds {ms}; Write-Output '{marker}'\"";
        }

        return $"sleep {seconds.ToString(CultureInfo.InvariantCulture)}; echo {marker}";
    }

    private static string SleepCommand(int seconds)
    {
        if (OperatingSystem.IsWindows())
            return $"powershell -NoProfile -Command \"Start-Sleep -Seconds {seconds}\"";

        return $"sleep {seconds}";
    }

    private static string ReadAndEchoCommand()
    {
        if (OperatingSystem.IsWindows())
            return "powershell -NoProfile -Command \"$line = [Console]::In.ReadLine(); Write-Output $line\"";

        return "read line; echo $line";
    }

    private static string LongEchoCommand()
    {
        if (OperatingSystem.IsWindows())
            return "powershell -NoProfile -Command \"1..40 | ForEach-Object { Write-Output '0123456789' }\"";

        return "for i in $(seq 1 40); do echo 0123456789; done";
    }

    private static string BuildMetadataPath(string root, string scopeKey, string sessionId)
    {
#pragma warning disable CA1308 // Match manager's lowercase hash path convention exactly.
        var scopeHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scopeKey)))
            .ToLowerInvariant();
#pragma warning restore CA1308
        return Path.Combine(root, $"{scopeHash}-{sessionId}.json");
    }
}
