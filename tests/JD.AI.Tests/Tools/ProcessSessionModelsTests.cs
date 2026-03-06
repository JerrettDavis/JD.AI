using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class ProcessSessionModelsTests
{
    // ── ProcessSessionStatus enum ────────────────────────────────────────

    [Theory]
    [InlineData(ProcessSessionStatus.Running, 0)]
    [InlineData(ProcessSessionStatus.Completed, 1)]
    [InlineData(ProcessSessionStatus.Failed, 2)]
    [InlineData(ProcessSessionStatus.TimedOut, 3)]
    [InlineData(ProcessSessionStatus.Killed, 4)]
    [InlineData(ProcessSessionStatus.Orphaned, 5)]
    public void ProcessSessionStatus_Values(ProcessSessionStatus status, int expected) =>
        ((int)status).Should().Be(expected);

    // ── ProcessExecRequest record ────────────────────────────────────────

    [Fact]
    public void ProcessExecRequest_Defaults()
    {
        var req = new ProcessExecRequest("echo hello");
        req.Command.Should().Be("echo hello");
        req.WorkingDirectory.Should().BeNull();
        req.YieldMs.Should().Be(250);
        req.Background.Should().BeFalse();
        req.TimeoutMs.Should().Be(60_000);
        req.Pty.Should().BeFalse();
        req.Host.Should().Be("local");
    }

    [Fact]
    public void ProcessExecRequest_AllParameters()
    {
        var req = new ProcessExecRequest(
            Command: "ls -la",
            WorkingDirectory: "/tmp",
            YieldMs: 100,
            Background: true,
            TimeoutMs: 30_000,
            Pty: true,
            Host: "remote-1");

        req.Command.Should().Be("ls -la");
        req.WorkingDirectory.Should().Be("/tmp");
        req.YieldMs.Should().Be(100);
        req.Background.Should().BeTrue();
        req.TimeoutMs.Should().Be(30_000);
        req.Pty.Should().BeTrue();
        req.Host.Should().Be("remote-1");
    }

    [Fact]
    public void ProcessExecRequest_RecordEquality()
    {
        var a = new ProcessExecRequest("cmd");
        var b = new ProcessExecRequest("cmd");
        a.Should().Be(b);
    }

    [Fact]
    public void ProcessExecRequest_RecordInequality()
    {
        var a = new ProcessExecRequest("cmd1");
        var b = new ProcessExecRequest("cmd2");
        a.Should().NotBe(b);
    }

    // ── ProcessSessionSnapshot record ────────────────────────────────────

    [Fact]
    public void ProcessSessionSnapshot_Construction()
    {
        var now = DateTimeOffset.UtcNow;
        var snap = new ProcessSessionSnapshot(
            SessionId: "proc-1",
            ScopeKey: "scope-a",
            Command: "echo test",
            WorkingDirectory: "/home",
            Pty: false,
            Host: "local",
            Status: ProcessSessionStatus.Completed,
            ProcessId: 1234,
            ExitCode: 0,
            FailureReason: null,
            StartedAtUtc: now,
            EndedAtUtc: now.AddSeconds(2),
            StdoutChars: 100,
            StderrChars: 0);

        snap.SessionId.Should().Be("proc-1");
        snap.ScopeKey.Should().Be("scope-a");
        snap.Status.Should().Be(ProcessSessionStatus.Completed);
        snap.ProcessId.Should().Be(1234);
        snap.ExitCode.Should().Be(0);
        snap.FailureReason.Should().BeNull();
        snap.StdoutChars.Should().Be(100);
        snap.StderrChars.Should().Be(0);
    }

    [Fact]
    public void ProcessSessionSnapshot_RecordEquality()
    {
        var now = DateTimeOffset.UtcNow;
        var a = new ProcessSessionSnapshot("s1", "k1", "cmd", null, false, "local",
            ProcessSessionStatus.Running, null, null, null, now, null, 0, 0);
        var b = new ProcessSessionSnapshot("s1", "k1", "cmd", null, false, "local",
            ProcessSessionStatus.Running, null, null, null, now, null, 0, 0);
        a.Should().Be(b);
    }

    // ── ProcessSessionLogs record ────────────────────────────────────────

    [Fact]
    public void ProcessSessionLogs_Construction()
    {
        var logs = new ProcessSessionLogs(
            SessionId: "proc-2",
            Stdout: "output text",
            Stderr: "",
            StdoutChars: 11,
            StderrChars: 0);

        logs.SessionId.Should().Be("proc-2");
        logs.Stdout.Should().Be("output text");
        logs.Stderr.Should().BeEmpty();
        logs.StdoutChars.Should().Be(11);
        logs.StderrChars.Should().Be(0);
    }

    [Fact]
    public void ProcessSessionLogs_RecordEquality()
    {
        var a = new ProcessSessionLogs("s1", "out", "err", 3, 3);
        var b = new ProcessSessionLogs("s1", "out", "err", 3, 3);
        a.Should().Be(b);
    }
}
