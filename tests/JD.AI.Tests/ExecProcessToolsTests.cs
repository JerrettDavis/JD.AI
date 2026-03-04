using System.Globalization;
using System.Text.Json;
using JD.AI.Core.Tools;
using JD.AI.Core.Tracing;

namespace JD.AI.Tests;

public sealed class ExecProcessToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ProcessSessionManager _manager;
    private readonly ExecProcessTools _tools;

    public ExecProcessToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-exec-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _manager = new ProcessSessionManager(
            metadataRoot: _tempDir,
            completedRetention: TimeSpan.FromMinutes(30));
        _tools = new ExecProcessTools(_manager);
    }

    [Fact]
    public async Task ExecAsync_RejectsInvalidInputs()
    {
        var empty = await _tools.ExecAsync(command: "", host: "local");
        Assert.False(ReadBool(empty, "ok"));

        var badHost = await _tools.ExecAsync(command: "echo hi", host: "remote");
        Assert.False(ReadBool(badHost, "ok"));

        var badYield = await _tools.ExecAsync(command: "echo hi", yieldMs: -1);
        Assert.False(ReadBool(badYield, "ok"));

        var badTimeout = await _tools.ExecAsync(command: "echo hi", timeoutMs: -1);
        Assert.False(ReadBool(badTimeout, "ok"));
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidActions()
    {
        var unknown = await _tools.ProcessAsync(action: "unknown");
        Assert.False(ReadBool(unknown, "ok"));

        var missingId = await _tools.ProcessAsync(action: "poll");
        Assert.False(ReadBool(missingId, "ok"));

        var badLog = await _tools.ProcessAsync(action: "log", id: "proc-1", maxChars: 0);
        Assert.False(ReadBool(badLog, "ok"));

        var missingWriteInput = await _tools.ProcessAsync(action: "write", id: "proc-1");
        Assert.False(ReadBool(missingWriteInput, "ok"));

        var missingLogId = await _tools.ProcessAsync(action: "log");
        Assert.False(ReadBool(missingLogId, "ok"));

        var missingWriteId = await _tools.ProcessAsync(action: "write", input: "x");
        Assert.False(ReadBool(missingWriteId, "ok"));

        var missingKillId = await _tools.ProcessAsync(action: "kill");
        Assert.False(ReadBool(missingKillId, "ok"));

        var missingRemoveId = await _tools.ProcessAsync(action: "remove");
        Assert.False(ReadBool(missingRemoveId, "ok"));

        var pollNegativeYield = await _tools.ProcessAsync(action: "poll", id: "proc-1", yieldMs: -1);
        Assert.False(ReadBool(pollNegativeYield, "ok"));

        var pollUnknown = await _tools.ProcessAsync(action: "poll", id: "proc-999999", yieldMs: 0);
        Assert.False(ReadBool(pollUnknown, "ok"));

        var logUnknown = await _tools.ProcessAsync(action: "log", id: "proc-999999", maxChars: 100);
        Assert.False(ReadBool(logUnknown, "ok"));

        var writeUnknown = await _tools.ProcessAsync(action: "write", id: "proc-999999", input: "x");
        Assert.False(ReadBool(writeUnknown, "ok"));

        var killUnknown = await _tools.ProcessAsync(action: "kill", id: "proc-999999");
        Assert.False(ReadBool(killUnknown, "ok"));

        var removeUnknown = await _tools.ProcessAsync(action: "remove", id: "proc-999999");
        Assert.False(ReadBool(removeUnknown, "ok"));
    }

    [Fact]
    public async Task ExecAndProcessFlow_ListPollLogKillRemove()
    {
        TraceContext.CurrentContext = new JD.AI.Core.Tracing.ExecutionContext
        {
            SessionId = "tools-session",
            ParentAgentId = "agent-a",
        };

        var exec = await _tools.ExecAsync(
            command: SleepThenEcho("hello-proc", 0.25),
            background: true,
            yieldMs: 0,
            timeoutMs: 5_000);
        Assert.True(ReadBool(exec, "ok"));

        var sessionId = ReadString(exec, "session", "SessionId");
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var list = await _tools.ProcessAsync(action: "list");
        Assert.True(ReadBool(list, "ok"));
        Assert.True(ReadInt(list, "count") >= 1);

        var poll = await _tools.ProcessAsync(action: "poll", id: sessionId, yieldMs: 2_000);
        Assert.True(ReadBool(poll, "ok"));

        var log = await _tools.ProcessAsync(action: "log", id: sessionId, maxChars: 2_000);
        Assert.True(ReadBool(log, "ok"));
        Assert.Contains("hello-proc", log, StringComparison.OrdinalIgnoreCase);

        var kill = await _tools.ProcessAsync(action: "kill", id: sessionId);
        Assert.True(ReadBool(kill, "ok"));

        var remove = await _tools.ProcessAsync(action: "remove", id: sessionId, force: true);
        Assert.True(ReadBool(remove, "ok"));

        var clear = await _tools.ProcessAsync(action: "clear", force: true);
        Assert.True(ReadBool(clear, "ok"));
    }

    [Fact]
    public async Task ProcessWrite_WritesInputToRunningSession()
    {
        TraceContext.CurrentContext = new JD.AI.Core.Tracing.ExecutionContext
        {
            SessionId = "tools-session",
            ParentAgentId = "agent-a",
        };

        var exec = await _tools.ExecAsync(
            command: ReadAndEchoCommand(),
            background: true,
            yieldMs: 0,
            timeoutMs: 5_000);
        var sessionId = ReadString(exec, "session", "SessionId");

        var write = await _tools.ProcessAsync(action: "write", id: sessionId, input: "tool-stdin");
        Assert.True(ReadBool(write, "ok"));

        var poll = await _tools.ProcessAsync(action: "poll", id: sessionId, yieldMs: 2_000);
        Assert.True(ReadBool(poll, "ok"));

        var log = await _tools.ProcessAsync(action: "log", id: sessionId, maxChars: 2_000);
        Assert.True(ReadBool(log, "ok"));
        Assert.Contains("tool-stdin", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveScopeKey_UsesDefaultsWhenTraceContextIsEmpty()
    {
        TraceContext.CurrentContext = new JD.AI.Core.Tracing.ExecutionContext();
        var scope = ExecProcessTools.ResolveScopeKey();
        Assert.Equal("session:default::agent:root", scope);
    }

    public void Dispose()
    {
        TraceContext.CurrentContext = JD.AI.Core.Tracing.ExecutionContext.Empty;
        _manager.Clear("tools-session::agent-a", includeRunning: true);

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup for test temp files.
        }
    }

    private static bool ReadBool(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(property).GetBoolean();
    }

    private static int ReadInt(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(property).GetInt32();
    }

    private static string ReadString(string json, string parent, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(parent).GetProperty(property).GetString() ?? string.Empty;
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

    private static string ReadAndEchoCommand()
    {
        if (OperatingSystem.IsWindows())
            return "powershell -NoProfile -Command \"$line = [Console]::In.ReadLine(); Write-Output $line\"";

        return "read line; echo $line";
    }
}
