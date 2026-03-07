using FluentAssertions;
using JD.AI.Core.Agents.Orchestration;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class TeamContextToolsTests
{
    private static TeamContext CreateContext(string goal = "test goal") =>
        new(goal);

    // ── ReadScratchpad ─────────────────────────────────────────────────

    [Fact]
    public void ReadScratchpad_KeyNotFound_ReturnsNotFound()
    {
        var ctx = CreateContext();
        var tools = new TeamContextTools(ctx, "agent-1");

        tools.ReadScratchpad("missing").Should().Be("(not found)");
    }

    [Fact]
    public void ReadScratchpad_KeyExists_ReturnsValue()
    {
        var ctx = CreateContext();
        ctx.WriteScratchpad("key1", "value1");
        var tools = new TeamContextTools(ctx, "agent-1");

        tools.ReadScratchpad("key1").Should().Be("value1");
    }

    // ── WriteScratchpad ────────────────────────────────────────────────

    [Fact]
    public void WriteScratchpad_StoresValue()
    {
        var ctx = CreateContext();
        var tools = new TeamContextTools(ctx, "agent-1");

        var result = tools.WriteScratchpad("k", "v");

        result.Should().Contain("Stored 'k'");
        ctx.ReadScratchpad("k").Should().Be("v");
    }

    [Fact]
    public void WriteScratchpad_RecordsEvent()
    {
        var ctx = CreateContext();
        var tools = new TeamContextTools(ctx, "writer");

        tools.WriteScratchpad("key", "value");

        var events = ctx.GetEventsSnapshot();
        events.Should().Contain(e =>
            e.AgentName == "writer" &&
            e.EventType == AgentEventType.Decision &&
            e.Content.Contains("key", StringComparison.Ordinal));
    }

    // ── LogFinding ─────────────────────────────────────────────────────

    [Fact]
    public void LogFinding_RecordsEvent()
    {
        var ctx = CreateContext();
        var tools = new TeamContextTools(ctx, "observer");

        var result = tools.LogFinding("found a bug");

        result.Should().Be("Finding logged.");
        var events = ctx.GetEventsSnapshot();
        events.Should().Contain(e =>
            e.AgentName == "observer" &&
            e.EventType == AgentEventType.Finding &&
            e.Content == "found a bug");
    }

    // ── GetEventLog ────────────────────────────────────────────────────

    [Fact]
    public void GetEventLog_NoEvents_ReturnsMessage()
    {
        var ctx = CreateContext();
        var tools = new TeamContextTools(ctx, "agent");

        tools.GetEventLog().Should().Be("No events recorded yet.");
    }

    [Fact]
    public void GetEventLog_WithEvents_ReturnsFormattedLog()
    {
        var ctx = CreateContext();
        ctx.RecordEvent("a1", AgentEventType.Started, "starting");
        ctx.RecordEvent("a2", AgentEventType.Finding, "found issue");
        var tools = new TeamContextTools(ctx, "reader");

        var log = tools.GetEventLog();

        log.Should().Contain("a1");
        log.Should().Contain("Started");
        log.Should().Contain("a2");
        log.Should().Contain("found issue");
    }

    // ── GetTeamGoal ────────────────────────────────────────────────────

    [Fact]
    public void GetTeamGoal_ReturnsGoal()
    {
        var ctx = CreateContext("build the feature");
        var tools = new TeamContextTools(ctx, "agent");

        tools.GetTeamGoal().Should().Be("build the feature");
    }

    // ── GetAgentResult ─────────────────────────────────────────────────

    [Fact]
    public void GetAgentResult_NotCompleted_ReturnsMessage()
    {
        var ctx = CreateContext();
        var tools = new TeamContextTools(ctx, "reader");

        tools.GetAgentResult("missing-agent")
            .Should().Contain("has not completed yet");
    }

    [Fact]
    public void GetAgentResult_Completed_ReturnsOutput()
    {
        var ctx = CreateContext();
        ctx.SetResult(new AgentResult { AgentName = "worker", Output = "42" });
        var tools = new TeamContextTools(ctx, "reader");

        tools.GetAgentResult("worker").Should().Be("42");
    }
}
