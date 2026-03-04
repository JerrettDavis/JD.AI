using FluentAssertions;
using JD.AI.Core.Safety;

namespace JD.AI.Tests.Safety;

public sealed class ToolLoopDetectorTests
{
    // ── Exact repetition detection ───────────────────────

    [Fact]
    public void NoRepetition_ReturnsOk()
    {
        var detector = new ToolLoopDetector();

        var result = detector.RecordAndEvaluate("read_file", "path=foo.txt");

        result.Decision.Should().Be(LoopDecision.None);
    }

    [Fact]
    public void BelowWarningThreshold_ReturnsOk()
    {
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 3);

        detector.RecordAndEvaluate("read_file", "path=foo.txt");
        var result = detector.RecordAndEvaluate("read_file", "path=foo.txt");

        result.Decision.Should().Be(LoopDecision.None);
    }

    [Fact]
    public void AtWarningThreshold_ReturnsWarning()
    {
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 3);

        detector.RecordAndEvaluate("read_file", "path=foo.txt");
        detector.RecordAndEvaluate("read_file", "path=foo.txt");
        var result = detector.RecordAndEvaluate("read_file", "path=foo.txt");

        result.Decision.Should().Be(LoopDecision.Warning);
        result.Type.Should().Be(LoopType.ExactRepetition);
        result.Message.Should().Contain("read_file");
    }

    [Fact]
    public void AtHardStopThreshold_ReturnsHardStop()
    {
        var detector = new ToolLoopDetector(
            repetitionWarningThreshold: 3,
            repetitionHardStopThreshold: 5);

        for (var i = 0; i < 4; i++)
            detector.RecordAndEvaluate("write_file", "path=bar.txt");

        var result = detector.RecordAndEvaluate("write_file", "path=bar.txt");

        result.Decision.Should().Be(LoopDecision.HardStop);
        result.Type.Should().Be(LoopType.ExactRepetition);
    }

    [Fact]
    public void DifferentArgs_DoNotTriggerRepetition()
    {
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 2);

        detector.RecordAndEvaluate("read_file", "path=a.txt");
        detector.RecordAndEvaluate("read_file", "path=b.txt");
        var result = detector.RecordAndEvaluate("read_file", "path=c.txt");

        result.Decision.Should().Be(LoopDecision.None);
    }

    [Fact]
    public void DifferentTools_DoNotTriggerRepetition()
    {
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 2);

        detector.RecordAndEvaluate("read_file", "path=foo.txt");
        var result = detector.RecordAndEvaluate("write_file", "path=foo.txt");

        result.Decision.Should().Be(LoopDecision.None);
    }

    // ── No-progress output detection ─────────────────────

    [Fact]
    public void SameOutputRepeated_ReturnsWarning()
    {
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 3);

        // Different args but same tool and same output fingerprint
        detector.RecordAndEvaluate("grep", "args1", outputFingerprint: "hash123");
        detector.RecordAndEvaluate("grep", "args1", outputFingerprint: "hash123");
        var result = detector.RecordAndEvaluate("grep", "args1", outputFingerprint: "hash123");

        result.Decision.Should().Be(LoopDecision.Warning);
    }

    // ── Ping-pong detection ──────────────────────────────

    [Fact]
    public void PingPongPattern_DetectedAtThreshold()
    {
        var detector = new ToolLoopDetector(pingPongThreshold: 4);

        detector.RecordAndEvaluate("read_file", "a");
        detector.RecordAndEvaluate("write_file", "b");
        detector.RecordAndEvaluate("read_file", "c");
        detector.RecordAndEvaluate("write_file", "d");
        detector.RecordAndEvaluate("read_file", "e");
        detector.RecordAndEvaluate("write_file", "f");
        detector.RecordAndEvaluate("read_file", "g");
        var result = detector.RecordAndEvaluate("write_file", "h");

        result.Decision.Should().Be(LoopDecision.HardStop);
        result.Type.Should().Be(LoopType.PingPong);
    }

    [Fact]
    public void NonAlternatingPattern_DoesNotTriggerPingPong()
    {
        var detector = new ToolLoopDetector(pingPongThreshold: 4);

        detector.RecordAndEvaluate("read_file", "a");
        detector.RecordAndEvaluate("write_file", "b");
        detector.RecordAndEvaluate("grep", "c"); // Breaks the pattern
        var result = detector.RecordAndEvaluate("write_file", "d");

        result.Decision.Should().Be(LoopDecision.None);
    }

    // ── Cross-agent ping-pong ────────────────────────────

    [Fact]
    public void CrossAgentPingPong_Detected()
    {
        var detector = new ToolLoopDetector(pingPongThreshold: 3);

        detector.RecordAndEvaluate("spawn_agent", "a", agentId: "agent-1");
        detector.RecordAndEvaluate("spawn_agent", "b", agentId: "agent-2");
        detector.RecordAndEvaluate("spawn_agent", "c", agentId: "agent-1");
        var result = detector.RecordAndEvaluate("spawn_agent", "d", agentId: "agent-2");

        result.Decision.Should().Be(LoopDecision.Warning);
        result.Type.Should().Be(LoopType.CrossAgentPingPong);
    }

    // ── Window management ────────────────────────────────

    [Fact]
    public void WindowTrims_OldEntriesPurged()
    {
        var detector = new ToolLoopDetector(
            windowSize: 5,
            repetitionWarningThreshold: 3);

        // Fill window with the same call
        detector.RecordAndEvaluate("read_file", "x");
        detector.RecordAndEvaluate("read_file", "x");

        // Push them out of window with different calls
        for (var i = 0; i < 5; i++)
            detector.RecordAndEvaluate("grep", $"arg{i}");

        // Now this should be fresh — no repetition from old entries
        var result = detector.RecordAndEvaluate("read_file", "x");
        result.Decision.Should().Be(LoopDecision.None);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 2);

        detector.RecordAndEvaluate("read_file", "x");
        detector.Reset();
        var result = detector.RecordAndEvaluate("read_file", "x");

        result.Decision.Should().Be(LoopDecision.None);
        detector.CurrentWindowCount.Should().Be(1);
    }

    // ── False positive prevention (valid polling) ────────

    [Fact]
    public void ValidPollingWorkflow_NotFlaggedAsLoop()
    {
        // Simulates: check status → do work → check status → do work
        // Different args each time = not a loop
        var detector = new ToolLoopDetector(repetitionWarningThreshold: 3);

        detector.RecordAndEvaluate("run_command", "git status");
        detector.RecordAndEvaluate("write_file", "path=src/main.cs");
        detector.RecordAndEvaluate("run_command", "dotnet build");
        detector.RecordAndEvaluate("read_file", "path=errors.log");
        detector.RecordAndEvaluate("write_file", "path=src/fix.cs");
        var result = detector.RecordAndEvaluate("run_command", "dotnet test");

        result.Decision.Should().Be(LoopDecision.None);
    }
}
