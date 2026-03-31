using FluentAssertions;
using JD.AI.Core.Events;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Events;

/// <summary>
/// Tests for <see cref="ToolAuditEntry"/> and its factory method mapping
/// <see cref="ToolExecutionGateDecision"/> to human-readable decision strings.
/// </summary>
public sealed class ToolAuditEntryTests
{
    [Fact]
    public void Create_BlockedDecision_MapsToDenied()
    {
        var entry = ToolAuditEntry.Create(
            toolName: "git_push",
            arguments: "force=true",
            result: null,
            decision: ToolExecutionGateDecision.Blocked,
            durationMs: 15,
            sessionId: "sess-blocked");

        entry.ToolName.Should().Be("git_push");
        entry.Arguments.Should().Be("force=true");
        entry.Decision.Should().Be("Denied");
        entry.DurationMs.Should().Be(15);
        entry.SessionId.Should().Be("sess-blocked");
        entry.EventType.Should().Be("tool.audit");
    }

    [Fact]
    public void Create_AllowWithoutPrompt_MapsToAllowed()
    {
        var entry = ToolAuditEntry.Create(
            toolName: "read_file",
            arguments: "path=/tmp/README.md",
            result: "file contents...",
            decision: ToolExecutionGateDecision.AllowWithoutPrompt,
            durationMs: 8,
            sessionId: "sess-allow");

        entry.Decision.Should().Be("Allowed");
    }

    [Fact]
    public void Create_RequirePrompt_MapsToPrompted()
    {
        var entry = ToolAuditEntry.Create(
            toolName: "run_command",
            arguments: "cmd=ls -la",
            result: null,
            decision: ToolExecutionGateDecision.RequirePrompt,
            durationMs: 0,
            sessionId: "sess-prompt");

        entry.Decision.Should().Be("Prompted");
    }

    [Fact]
    public void Create_SetsTimestampToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var entry = ToolAuditEntry.Create(
            "write_file", null, null,
            ToolExecutionGateDecision.AllowWithoutPrompt,
            1, "sess-time");
        var after = DateTimeOffset.UtcNow;

        entry.Timestamp.Should().BeOnOrAfter(before);
        entry.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_SetsSourceId_ToSessionId()
    {
        var entry = ToolAuditEntry.Create(
            "write_file", null, null,
            ToolExecutionGateDecision.AllowWithoutPrompt,
            3, "sess-source");

        entry.SourceId.Should().Be("sess-source");
    }

    [Fact]
    public void Create_NullArguments_Handled()
    {
        var entry = ToolAuditEntry.Create(
            "tool_no_args", null, "no args",
            ToolExecutionGateDecision.AllowWithoutPrompt,
            1, "sess-null");

        entry.Arguments.Should().BeNull();
        entry.Result.Should().Be("no args");
    }
}