using JD.AI.Tui.Agent.Orchestration;

namespace JD.AI.Tui.Tests;

public sealed class AgentEventTests
{
    [Fact]
    public void AgentEvent_ShortConstructor_SetsTimestamp()
    {
        var before = DateTime.UtcNow;
        var evt = new AgentEvent("agent1", AgentEventType.Started, "hello");
        var after = DateTime.UtcNow;

        Assert.Equal("agent1", evt.AgentName);
        Assert.Equal(AgentEventType.Started, evt.EventType);
        Assert.Equal("hello", evt.Content);
        Assert.InRange(evt.Timestamp, before, after);
    }

    [Fact]
    public void AgentEvent_FullConstructor_UsesProvidedTimestamp()
    {
        var ts = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var evt = new AgentEvent("agent1", AgentEventType.Completed, "done", ts);

        Assert.Equal(ts, evt.Timestamp);
    }

    [Theory]
    [InlineData(AgentEventType.Started)]
    [InlineData(AgentEventType.Thinking)]
    [InlineData(AgentEventType.ToolCall)]
    [InlineData(AgentEventType.Finding)]
    [InlineData(AgentEventType.Decision)]
    [InlineData(AgentEventType.Error)]
    [InlineData(AgentEventType.Completed)]
    [InlineData(AgentEventType.Cancelled)]
    public void AgentEventType_AllValuesAreDefined(AgentEventType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    [Theory]
    [InlineData(SubagentStatus.Pending)]
    [InlineData(SubagentStatus.Started)]
    [InlineData(SubagentStatus.Thinking)]
    [InlineData(SubagentStatus.ExecutingTool)]
    [InlineData(SubagentStatus.Completed)]
    [InlineData(SubagentStatus.Failed)]
    [InlineData(SubagentStatus.Cancelled)]
    public void SubagentStatus_AllValuesAreDefined(SubagentStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }

    [Fact]
    public void SubagentProgress_CreatesWithDefaults()
    {
        var progress = new SubagentProgress("agent1", SubagentStatus.Completed);

        Assert.Equal("agent1", progress.AgentName);
        Assert.Equal(SubagentStatus.Completed, progress.Status);
        Assert.Null(progress.Detail);
        Assert.Null(progress.TokensUsed);
        Assert.Null(progress.Elapsed);
    }

    [Fact]
    public void SubagentProgress_WithAllFields()
    {
        var elapsed = TimeSpan.FromSeconds(1.5);
        var progress = new SubagentProgress(
            "agent1", SubagentStatus.Completed, "done", 100, elapsed);

        Assert.Equal("done", progress.Detail);
        Assert.Equal(100, progress.TokensUsed);
        Assert.Equal(elapsed, progress.Elapsed);
    }

    [Fact]
    public void AgentResult_DefaultsToSuccess()
    {
        var result = new AgentResult { AgentName = "test", Output = "output" };

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(0, result.TokensUsed);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void AgentResult_CanSetFailure()
    {
        var result = new AgentResult
        {
            AgentName = "test",
            Output = "",
            Success = false,
            Error = "something broke",
        };

        Assert.False(result.Success);
        Assert.Equal("something broke", result.Error);
    }

    [Fact]
    public void TeamResult_TotalTokens_SumsAgents()
    {
        var result = new TeamResult
        {
            Output = "final",
            Strategy = "fan-out",
            AgentResults = new Dictionary<string, AgentResult>(StringComparer.Ordinal)
            {
                ["a"] = new AgentResult { AgentName = "a", Output = "x", TokensUsed = 100 },
                ["b"] = new AgentResult { AgentName = "b", Output = "y", TokensUsed = 200 },
            },
        };

        Assert.Equal(300, result.TotalTokens);
    }
}
