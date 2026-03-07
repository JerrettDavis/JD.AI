using FluentAssertions;
using JD.AI.Core.Agents.Orchestration;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class TeamContextTests
{
    // ── Scratchpad ───────────────────────────────────────────

    [Fact]
    public void ReadScratchpad_Missing_ReturnsNull()
    {
        var ctx = new TeamContext("goal");
        ctx.ReadScratchpad("missing").Should().BeNull();
    }

    [Fact]
    public void WriteScratchpad_ThenRead_ReturnsValue()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("key", "value");
        ctx.ReadScratchpad("key").Should().Be("value");
    }

    [Fact]
    public void WriteScratchpad_Overwrites_PreviousValue()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("key", "old");
        ctx.WriteScratchpad("key", "new");
        ctx.ReadScratchpad("key").Should().Be("new");
    }

    [Fact]
    public void RemoveScratchpad_ExistingKey_ReturnsTrue()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("key", "val");
        ctx.RemoveScratchpad("key").Should().BeTrue();
        ctx.ReadScratchpad("key").Should().BeNull();
    }

    [Fact]
    public void RemoveScratchpad_MissingKey_ReturnsFalse()
    {
        var ctx = new TeamContext("goal");
        ctx.RemoveScratchpad("nope").Should().BeFalse();
    }

    [Fact]
    public void GetScratchpadSnapshot_ReturnsAllEntries()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("a", "1");
        ctx.WriteScratchpad("b", "2");

        var snapshot = ctx.GetScratchpadSnapshot();
        snapshot.Should().HaveCount(2);
        snapshot["a"].Should().Be("1");
        snapshot["b"].Should().Be("2");
    }

    // ── Events ──────────────────────────────────────────────

    [Fact]
    public void EventCount_InitiallyZero()
    {
        var ctx = new TeamContext("goal");
        ctx.EventCount.Should().Be(0);
    }

    [Fact]
    public void RecordEvent_IncrementsCount()
    {
        var ctx = new TeamContext("goal");
        ctx.RecordEvent("agent", AgentEventType.Started, "begin");
        ctx.EventCount.Should().Be(1);
    }

    [Fact]
    public void RecordEvent_WithAgentEvent_StoresEvent()
    {
        var ctx = new TeamContext("goal");
        var evt = new AgentEvent("agent1", AgentEventType.Decision, "chose A");
        ctx.RecordEvent(evt);

        var events = ctx.GetEventsSnapshot();
        events.Should().ContainSingle();
        events[0].AgentName.Should().Be("agent1");
        events[0].Content.Should().Be("chose A");
    }

    [Fact]
    public void GetEventsFor_FiltersbyAgent()
    {
        var ctx = new TeamContext("goal");
        ctx.RecordEvent("agent1", AgentEventType.Started, "a1 start");
        ctx.RecordEvent("agent2", AgentEventType.Started, "a2 start");
        ctx.RecordEvent("agent1", AgentEventType.Completed, "a1 done");

        var a1Events = ctx.GetEventsFor("agent1");
        a1Events.Should().HaveCount(2);
        a1Events.Should().AllSatisfy(e => e.AgentName.Should().Be("agent1"));
    }

    [Fact]
    public void GetEventsSnapshot_ReturnsChronologicalOrder()
    {
        var ctx = new TeamContext("goal");
        ctx.RecordEvent("a", AgentEventType.Started, "first");
        ctx.RecordEvent("b", AgentEventType.Started, "second");
        ctx.RecordEvent("c", AgentEventType.Started, "third");

        var events = ctx.GetEventsSnapshot();
        events.Should().HaveCount(3);
        events[0].Content.Should().Be("first");
        events[2].Content.Should().Be("third");
    }

    // ── Results ─────────────────────────────────────────────

    [Fact]
    public void GetResult_Missing_ReturnsNull()
    {
        var ctx = new TeamContext("goal");
        ctx.GetResult("missing").Should().BeNull();
    }

    [Fact]
    public void SetResult_ThenGet_ReturnsResult()
    {
        var ctx = new TeamContext("goal");
        var result = new AgentResult
        {
            AgentName = "agent1",
            Output = "done",
            Success = true,
        };
        ctx.SetResult(result);
        ctx.GetResult("agent1").Should().NotBeNull();
        ctx.GetResult("agent1")!.Output.Should().Be("done");
    }

    [Fact]
    public void GetResultsSnapshot_ReturnsAllResults()
    {
        var ctx = new TeamContext("goal");
        ctx.SetResult(new AgentResult { AgentName = "a", Output = "1", Success = true });
        ctx.SetResult(new AgentResult { AgentName = "b", Output = "2", Success = true });

        var snapshot = ctx.GetResultsSnapshot();
        snapshot.Should().HaveCount(2);
    }

    [Fact]
    public void AllCompleted_WhenAllPresent_ReturnsTrue()
    {
        var ctx = new TeamContext("goal");
        ctx.SetResult(new AgentResult { AgentName = "a", Output = "", Success = true });
        ctx.SetResult(new AgentResult { AgentName = "b", Output = "", Success = true });

        ctx.AllCompleted(["a", "b"]).Should().BeTrue();
    }

    [Fact]
    public void AllCompleted_WhenMissing_ReturnsFalse()
    {
        var ctx = new TeamContext("goal");
        ctx.SetResult(new AgentResult { AgentName = "a", Output = "", Success = true });

        ctx.AllCompleted(["a", "b"]).Should().BeFalse();
    }

    // ── Nesting ─────────────────────────────────────────────

    [Fact]
    public void CanNest_Default_ReturnsTrue()
    {
        var ctx = new TeamContext("goal");
        ctx.CanNest.Should().BeTrue();
    }

    [Fact]
    public void CanNest_AtMaxDepth_ReturnsFalse()
    {
        var ctx = new TeamContext("goal") { MaxDepth = 2, CurrentDepth = 2 };
        ctx.CanNest.Should().BeFalse();
    }

    [Fact]
    public void CreateChildContext_IncrementsDepth()
    {
        var parent = new TeamContext("parent goal") { MaxDepth = 3 };
        var child = parent.CreateChildContext("child goal");

        child.Goal.Should().Be("child goal");
        child.CurrentDepth.Should().Be(1);
        child.MaxDepth.Should().Be(3);
    }

    [Fact]
    public void CreateChildContext_NestedChild_IncrementsAgain()
    {
        var parent = new TeamContext("root") { MaxDepth = 3 };
        var child = parent.CreateChildContext("level1");
        var grandchild = child.CreateChildContext("level2");

        grandchild.CurrentDepth.Should().Be(2);
        grandchild.CanNest.Should().BeTrue();

        var greatGrandchild = grandchild.CreateChildContext("level3");
        greatGrandchild.CurrentDepth.Should().Be(3);
        greatGrandchild.CanNest.Should().BeFalse();
    }

    // ── Summary ─────────────────────────────────────────────

    [Fact]
    public void ToPromptSummary_ContainsGoal()
    {
        var ctx = new TeamContext("analyze logs");
        ctx.ToPromptSummary().Should().Contain("analyze logs");
    }

    [Fact]
    public void ToPromptSummary_IncludesScratchpadEntries()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("key1", "value1");

        var summary = ctx.ToPromptSummary();
        summary.Should().Contain("key1");
        summary.Should().Contain("value1");
    }

    [Fact]
    public void ToPromptSummary_IncludesRecentEvents()
    {
        var ctx = new TeamContext("goal");
        ctx.RecordEvent("agent1", AgentEventType.Decision, "chose path A");

        var summary = ctx.ToPromptSummary();
        summary.Should().Contain("agent1");
        summary.Should().Contain("chose path A");
    }

    [Fact]
    public void ToPromptSummary_TruncatesLongValues()
    {
        var ctx = new TeamContext("goal");
        ctx.WriteScratchpad("big", new string('x', 500));

        var summary = ctx.ToPromptSummary();
        summary.Should().Contain("...");
    }

    // ── Goal ────────────────────────────────────────────────

    [Fact]
    public void Goal_ReturnConstructorValue()
    {
        var ctx = new TeamContext("my goal");
        ctx.Goal.Should().Be("my goal");
    }
}
