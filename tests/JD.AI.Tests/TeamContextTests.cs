using JD.AI.Tui.Agent.Orchestration;

namespace JD.AI.Tui.Tests;

public sealed class TeamContextTests
{
    [Fact]
    public void WriteScratchpad_ReadScratchpad_RoundTrips()
    {
        var ctx = new TeamContext("test goal");
        ctx.WriteScratchpad("key1", "value1");

        Assert.Equal("value1", ctx.ReadScratchpad("key1"));
    }

    [Fact]
    public void ReadScratchpad_MissingKey_ReturnsNull()
    {
        var ctx = new TeamContext("test goal");
        Assert.Null(ctx.ReadScratchpad("nonexistent"));
    }

    [Fact]
    public void WriteScratchpad_OverwritesExisting()
    {
        var ctx = new TeamContext("test goal");
        ctx.WriteScratchpad("key1", "v1");
        ctx.WriteScratchpad("key1", "v2");

        Assert.Equal("v2", ctx.ReadScratchpad("key1"));
    }

    [Fact]
    public void RemoveScratchpad_RemovesEntry()
    {
        var ctx = new TeamContext("test goal");
        ctx.WriteScratchpad("key1", "v1");

        Assert.True(ctx.RemoveScratchpad("key1"));
        Assert.Null(ctx.ReadScratchpad("key1"));
    }

    [Fact]
    public void GetScratchpadSnapshot_ReturnsAllEntries()
    {
        var ctx = new TeamContext("test goal");
        ctx.WriteScratchpad("a", "1");
        ctx.WriteScratchpad("b", "2");

        var snapshot = ctx.GetScratchpadSnapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("1", snapshot["a"]);
    }

    [Fact]
    public void RecordEvent_IncreasesEventCount()
    {
        var ctx = new TeamContext("test goal");
        Assert.Equal(0, ctx.EventCount);

        ctx.RecordEvent("agent1", AgentEventType.Started, "started");

        Assert.Equal(1, ctx.EventCount);
    }

    [Fact]
    public void GetEventsSnapshot_ReturnsChronological()
    {
        var ctx = new TeamContext("test goal");
        ctx.RecordEvent("agent1", AgentEventType.Started, "first");
        ctx.RecordEvent("agent2", AgentEventType.Started, "second");

        var events = ctx.GetEventsSnapshot();

        Assert.Equal(2, events.Count);
        Assert.Equal("first", events[0].Content);
        Assert.Equal("second", events[1].Content);
    }

    [Fact]
    public void GetEventsFor_FiltersbyAgent()
    {
        var ctx = new TeamContext("test goal");
        ctx.RecordEvent("agent1", AgentEventType.Started, "a1");
        ctx.RecordEvent("agent2", AgentEventType.Started, "a2");
        ctx.RecordEvent("agent1", AgentEventType.Completed, "a1 done");

        var events = ctx.GetEventsFor("agent1");

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal("agent1", e.AgentName));
    }

    [Fact]
    public void SetResult_GetResult_RoundTrips()
    {
        var ctx = new TeamContext("test goal");
        var result = new AgentResult
        {
            AgentName = "agent1",
            Output = "some output",
        };

        ctx.SetResult(result);

        Assert.Equal("some output", ctx.GetResult("agent1")?.Output);
    }

    [Fact]
    public void GetResult_MissingAgent_ReturnsNull()
    {
        var ctx = new TeamContext("test goal");
        Assert.Null(ctx.GetResult("nonexistent"));
    }

    [Fact]
    public void AllCompleted_AllPresent_ReturnsTrue()
    {
        var ctx = new TeamContext("test goal");
        ctx.SetResult(new AgentResult { AgentName = "a", Output = "x" });
        ctx.SetResult(new AgentResult { AgentName = "b", Output = "y" });

        Assert.True(ctx.AllCompleted(["a", "b"]));
    }

    [Fact]
    public void AllCompleted_MissingAgent_ReturnsFalse()
    {
        var ctx = new TeamContext("test goal");
        ctx.SetResult(new AgentResult { AgentName = "a", Output = "x" });

        Assert.False(ctx.AllCompleted(["a", "b"]));
    }

    [Fact]
    public void CanNest_AtMaxDepth_ReturnsFalse()
    {
        var ctx = new TeamContext("test goal") { MaxDepth = 2, CurrentDepth = 2 };
        Assert.False(ctx.CanNest);
    }

    [Fact]
    public void CanNest_BelowMaxDepth_ReturnsTrue()
    {
        var ctx = new TeamContext("test goal") { MaxDepth = 2, CurrentDepth = 1 };
        Assert.True(ctx.CanNest);
    }

    [Fact]
    public void CreateChildContext_IncrementsDepth()
    {
        var ctx = new TeamContext("parent goal") { MaxDepth = 3, CurrentDepth = 0 };
        var child = ctx.CreateChildContext("child goal");

        Assert.Equal("child goal", child.Goal);
        Assert.Equal(1, child.CurrentDepth);
        Assert.Equal(3, child.MaxDepth);
    }

    [Fact]
    public void ToPromptSummary_IncludesGoal()
    {
        var ctx = new TeamContext("build the widget");
        var summary = ctx.ToPromptSummary();

        Assert.Contains("build the widget", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPromptSummary_IncludesScratchpad()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("key1", "val1");

        var summary = ctx.ToPromptSummary();

        Assert.Contains("key1", summary, StringComparison.Ordinal);
        Assert.Contains("val1", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ToPromptSummary_IncludesRecentEvents()
    {
        var ctx = new TeamContext("goal");
        ctx.RecordEvent("agent1", AgentEventType.Finding, "found bug");

        var summary = ctx.ToPromptSummary();

        Assert.Contains("found bug", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Goal_SetInConstructor()
    {
        var ctx = new TeamContext("my important goal");
        Assert.Equal("my important goal", ctx.Goal);
    }
}
