using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class SubagentPromptsTests
{
    // ── GetSystemPrompt ────────────────────────────────────────────────

    [Theory]
    [InlineData(SubagentType.Explore)]
    [InlineData(SubagentType.Task)]
    [InlineData(SubagentType.Plan)]
    [InlineData(SubagentType.Review)]
    [InlineData(SubagentType.General)]
    public void GetSystemPrompt_AllTypes_ReturnNonEmpty(SubagentType type)
    {
        SubagentPrompts.GetSystemPrompt(type).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetSystemPrompt_Unknown_ReturnsFallback()
    {
        SubagentPrompts.GetSystemPrompt((SubagentType)999)
            .Should().Contain("subagent");
    }

    [Fact]
    public void GetSystemPrompt_Explore_MentionsReadOnly()
    {
        SubagentPrompts.GetSystemPrompt(SubagentType.Explore)
            .Should().Contain("NOT modify");
    }

    [Fact]
    public void GetSystemPrompt_Task_MentionsCommands()
    {
        SubagentPrompts.GetSystemPrompt(SubagentType.Task)
            .Should().Contain("execute commands");
    }

    [Fact]
    public void GetSystemPrompt_Review_MentionsBugs()
    {
        SubagentPrompts.GetSystemPrompt(SubagentType.Review)
            .Should().Contain("Bugs");
    }

    // ── GetToolSet ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(SubagentType.Explore)]
    [InlineData(SubagentType.Task)]
    [InlineData(SubagentType.Plan)]
    [InlineData(SubagentType.Review)]
    [InlineData(SubagentType.General)]
    public void GetToolSet_AllTypes_ReturnNonEmpty(SubagentType type)
    {
        SubagentPrompts.GetToolSet(type).Should().NotBeEmpty();
    }

    [Fact]
    public void GetToolSet_Unknown_ReturnsEmpty()
    {
        SubagentPrompts.GetToolSet((SubagentType)999).Should().BeEmpty();
    }

    [Fact]
    public void GetToolSet_Explore_ContainsFileTools()
    {
        SubagentPrompts.GetToolSet(SubagentType.Explore).Should().Contain("FileTools");
    }

    [Fact]
    public void GetToolSet_Explore_DoesNotContainShellTools()
    {
        SubagentPrompts.GetToolSet(SubagentType.Explore).Should().NotContain("ShellTools");
    }

    [Fact]
    public void GetToolSet_Task_ContainsShellTools()
    {
        SubagentPrompts.GetToolSet(SubagentType.Task).Should().Contain("ShellTools");
    }

    [Fact]
    public void GetToolSet_General_ContainsAllCommonTools()
    {
        var tools = SubagentPrompts.GetToolSet(SubagentType.General);
        tools.Should().Contain("FileTools");
        tools.Should().Contain("SearchTools");
        tools.Should().Contain("GitTools");
        tools.Should().Contain("ShellTools");
        tools.Should().Contain("WebTools");
        tools.Should().Contain("MemoryTools");
    }

    [Fact]
    public void GetToolSet_General_HasMostTools()
    {
        var generalCount = SubagentPrompts.GetToolSet(SubagentType.General).Count;
        foreach (var type in Enum.GetValues<SubagentType>())
        {
            SubagentPrompts.GetToolSet(type).Count.Should()
                .BeLessThanOrEqualTo(generalCount,
                    $"{type} should not have more tools than General");
        }
    }

    [Fact]
    public void GetToolSet_ReturnsMutableSet()
    {
        // Each call returns a new HashSet that can be mutated (e.g. for AdditionalTools)
        var set1 = SubagentPrompts.GetToolSet(SubagentType.Explore);
        var set2 = SubagentPrompts.GetToolSet(SubagentType.Explore);
        set1.Should().NotBeSameAs(set2);
    }
}
