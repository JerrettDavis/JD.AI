using JD.AI.Tui.Agent;
using JD.AI.Tui.Agent.Orchestration;

namespace JD.AI.Tui.Tests;

public sealed class OrchestrationConfigTests
{
    [Fact]
    public void SubagentConfig_RequiredPropertiesSet()
    {
        var config = new SubagentConfig
        {
            Name = "test-agent",
            Prompt = "do something",
        };

        Assert.Equal("test-agent", config.Name);
        Assert.Equal("do something", config.Prompt);
        Assert.Equal(SubagentType.General, config.Type);
        Assert.Equal(10, config.MaxTurns);
        Assert.Null(config.SystemPrompt);
        Assert.Null(config.ModelId);
        Assert.Null(config.AdditionalTools);
        Assert.Null(config.Perspective);
    }

    [Fact]
    public void SubagentConfig_AllPropertiesSet()
    {
        var config = new SubagentConfig
        {
            Name = "explorer",
            Type = SubagentType.Explore,
            Prompt = "find the auth module",
            SystemPrompt = "custom prompt",
            MaxTurns = 5,
            ModelId = "gpt-4o",
            AdditionalTools = ["SpecialTool"],
            Perspective = "security-focused",
        };

        Assert.Equal(SubagentType.Explore, config.Type);
        Assert.Equal("custom prompt", config.SystemPrompt);
        Assert.Equal(5, config.MaxTurns);
        Assert.Equal("gpt-4o", config.ModelId);
        Assert.Single(config.AdditionalTools!);
        Assert.Equal("security-focused", config.Perspective);
    }

    [Theory]
    [InlineData(SubagentType.Explore)]
    [InlineData(SubagentType.Task)]
    [InlineData(SubagentType.Plan)]
    [InlineData(SubagentType.Review)]
    [InlineData(SubagentType.General)]
    public void SubagentPrompts_GetSystemPrompt_ReturnsNonEmpty(SubagentType type)
    {
        var prompt = SubagentPrompts.GetSystemPrompt(type);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }

    [Theory]
    [InlineData(SubagentType.Explore)]
    [InlineData(SubagentType.Task)]
    [InlineData(SubagentType.Plan)]
    [InlineData(SubagentType.Review)]
    [InlineData(SubagentType.General)]
    public void SubagentPrompts_GetToolSet_ReturnsNonEmpty(SubagentType type)
    {
        var toolSet = SubagentPrompts.GetToolSet(type);
        Assert.NotEmpty(toolSet);
    }

    [Fact]
    public void SubagentPrompts_Explore_HasReadOnlyTools()
    {
        var toolSet = SubagentPrompts.GetToolSet(SubagentType.Explore);
        Assert.Contains("FileTools", toolSet);
        Assert.Contains("SearchTools", toolSet);
        Assert.DoesNotContain("ShellTools", toolSet);
    }

    [Fact]
    public void SubagentPrompts_General_HasAllTools()
    {
        var toolSet = SubagentPrompts.GetToolSet(SubagentType.General);
        Assert.Contains("FileTools", toolSet);
        Assert.Contains("ShellTools", toolSet);
        Assert.Contains("WebTools", toolSet);
    }

    [Fact]
    public void SubagentPrompts_Task_HasShellTools()
    {
        var toolSet = SubagentPrompts.GetToolSet(SubagentType.Task);
        Assert.Contains("ShellTools", toolSet);
    }

    [Fact]
    public void TeamResult_DefaultsToSuccess()
    {
        var result = new TeamResult
        {
            Output = "done",
            Strategy = "sequential",
        };

        Assert.True(result.Success);
        Assert.Empty(result.AgentResults);
        Assert.Equal(0, result.TotalTokens);
    }

    [Fact]
    public void TeamContextTools_ReadWrite_RoundTrips()
    {
        var ctx = new TeamContext("goal");
        var tools = new TeamContextTools(ctx, "agent1");

        tools.WriteScratchpad("mykey", "myvalue");
        var value = tools.ReadScratchpad("mykey");

        Assert.Equal("myvalue", value);
    }

    [Fact]
    public void TeamContextTools_ReadMissing_ReturnsNotFound()
    {
        var ctx = new TeamContext("goal");
        var tools = new TeamContextTools(ctx, "agent1");

        Assert.Equal("(not found)", tools.ReadScratchpad("missing"));
    }

    [Fact]
    public void TeamContextTools_LogFinding_RecordsEvent()
    {
        var ctx = new TeamContext("goal");
        var tools = new TeamContextTools(ctx, "agent1");

        tools.LogFinding("found a bug");

        var events = ctx.GetEventsFor("agent1");
        Assert.Contains(events, e => e.EventType == AgentEventType.Finding);
    }

    [Fact]
    public void TeamContextTools_GetEventLog_ReturnsText()
    {
        var ctx = new TeamContext("goal");
        ctx.RecordEvent("agent1", AgentEventType.Started, "hello");
        var tools = new TeamContextTools(ctx, "agent2");

        var log = tools.GetEventLog();

        Assert.Contains("agent1", log, StringComparison.Ordinal);
        Assert.Contains("hello", log, StringComparison.Ordinal);
    }

    [Fact]
    public void TeamContextTools_GetTeamGoal_ReturnsGoal()
    {
        var ctx = new TeamContext("my important goal");
        var tools = new TeamContextTools(ctx, "agent1");

        Assert.Equal("my important goal", tools.GetTeamGoal());
    }

    [Fact]
    public void TeamContextTools_GetAgentResult_ReturnsOutput()
    {
        var ctx = new TeamContext("goal");
        ctx.SetResult(new AgentResult { AgentName = "a", Output = "result text" });
        var tools = new TeamContextTools(ctx, "b");

        Assert.Equal("result text", tools.GetAgentResult("a"));
    }

    [Fact]
    public void TeamContextTools_GetAgentResult_NotCompleted_ReturnsMessage()
    {
        var ctx = new TeamContext("goal");
        var tools = new TeamContextTools(ctx, "agent1");

        var result = tools.GetAgentResult("missing-agent");

        Assert.Contains("not completed yet", result, StringComparison.Ordinal);
    }
}
