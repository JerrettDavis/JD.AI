using FluentAssertions;
using JD.AI.Core.Agents.Orchestration;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class AgentResultTests
{
    [Fact]
    public void AgentResult_RequiredProperties()
    {
        var result = new AgentResult { AgentName = "explorer", Output = "Found 3 files" };
        result.AgentName.Should().Be("explorer");
        result.Output.Should().Be("Found 3 files");
    }

    [Fact]
    public void AgentResult_Defaults()
    {
        var result = new AgentResult { AgentName = "a", Output = "o" };
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.TokensUsed.Should().Be(0);
        result.Duration.Should().Be(TimeSpan.Zero);
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public void AgentResult_FailedWithError()
    {
        var result = new AgentResult
        {
            AgentName = "a",
            Output = "",
            Success = false,
            Error = "Connection timeout",
            TokensUsed = 500,
            Duration = TimeSpan.FromSeconds(30),
        };
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Connection timeout");
        result.TokensUsed.Should().Be(500);
    }

    [Fact]
    public void AgentResult_WithEvents()
    {
        var events = new[]
        {
            new AgentEvent("a", AgentEventType.Started, "Begin"),
            new AgentEvent("a", AgentEventType.Completed, "Done"),
        };
        var result = new AgentResult { AgentName = "a", Output = "ok", Events = events };
        result.Events.Should().HaveCount(2);
        result.Events[0].EventType.Should().Be(AgentEventType.Started);
    }

    // ── AgentEvent convenience constructor ────────────────────────────────

    [Fact]
    public void AgentEvent_ConvenienceConstructor_SetsTimestamp()
    {
        var evt = new AgentEvent("agent", AgentEventType.Finding, "found something");
        evt.AgentName.Should().Be("agent");
        evt.Content.Should().Be("found something");
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── SubagentProgress ──────────────────────────────────────────────────

    [Fact]
    public void SubagentProgress_RequiredProperties()
    {
        var progress = new SubagentProgress("agent1", SubagentStatus.Thinking);
        progress.AgentName.Should().Be("agent1");
        progress.Status.Should().Be(SubagentStatus.Thinking);
    }

    [Fact]
    public void SubagentProgress_OptionalDefaults()
    {
        var progress = new SubagentProgress("agent1", SubagentStatus.Pending);
        progress.Detail.Should().BeNull();
        progress.TokensUsed.Should().BeNull();
        progress.Elapsed.Should().BeNull();
    }

    [Fact]
    public void SubagentProgress_AllProperties()
    {
        var progress = new SubagentProgress(
            "agent1", SubagentStatus.ExecutingTool,
            Detail: "Running grep",
            TokensUsed: 1500,
            Elapsed: TimeSpan.FromSeconds(5));
        progress.Detail.Should().Be("Running grep");
        progress.TokensUsed.Should().Be(1500);
        progress.Elapsed.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SubagentProgress_RecordEquality()
    {
        var a = new SubagentProgress("a", SubagentStatus.Completed);
        var b = new SubagentProgress("a", SubagentStatus.Completed);
        a.Should().Be(b);
    }

    // ── AgentEventType enum ───────────────────────────────────────────────

    [Theory]
    [InlineData(AgentEventType.Started, 0)]
    [InlineData(AgentEventType.Thinking, 1)]
    [InlineData(AgentEventType.ToolCall, 2)]
    [InlineData(AgentEventType.Finding, 3)]
    [InlineData(AgentEventType.Decision, 4)]
    [InlineData(AgentEventType.Error, 5)]
    [InlineData(AgentEventType.Completed, 6)]
    [InlineData(AgentEventType.Cancelled, 7)]
    public void AgentEventType_Values(AgentEventType type, int expected) =>
        ((int)type).Should().Be(expected);

    // ── SubagentStatus enum ───────────────────────────────────────────────

    [Theory]
    [InlineData(SubagentStatus.Pending, 0)]
    [InlineData(SubagentStatus.Started, 1)]
    [InlineData(SubagentStatus.Thinking, 2)]
    [InlineData(SubagentStatus.ExecutingTool, 3)]
    [InlineData(SubagentStatus.Completed, 4)]
    [InlineData(SubagentStatus.Failed, 5)]
    [InlineData(SubagentStatus.Cancelled, 6)]
    public void SubagentStatus_Values(SubagentStatus status, int expected) =>
        ((int)status).Should().Be(expected);

    // ── CheckpointInfo ────────────────────────────────────────────────────

    [Fact]
    public void CheckpointInfo_Construction()
    {
        var ts = DateTime.UtcNow;
        var info = new JD.AI.Core.Agents.Checkpointing.CheckpointInfo("stash@{0}", "jdai-cp-before-edit", ts);
        info.Id.Should().Be("stash@{0}");
        info.Label.Should().Be("jdai-cp-before-edit");
        info.CreatedAt.Should().Be(ts);
    }

    [Fact]
    public void CheckpointInfo_RecordEquality()
    {
        var ts = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = new JD.AI.Core.Agents.Checkpointing.CheckpointInfo("id", "label", ts);
        var b = new JD.AI.Core.Agents.Checkpointing.CheckpointInfo("id", "label", ts);
        a.Should().Be(b);
    }
}
