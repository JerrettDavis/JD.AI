using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Agents;

public sealed class SubagentRunnerTests
{
    // ── GetLoadoutName ─────────────────────────────────────────────────

    [Theory]
    [InlineData(SubagentType.Explore, WellKnownLoadouts.Research)]
    [InlineData(SubagentType.Task, WellKnownLoadouts.Minimal)]
    [InlineData(SubagentType.Plan, WellKnownLoadouts.Developer)]
    [InlineData(SubagentType.Review, WellKnownLoadouts.Developer)]
    [InlineData(SubagentType.General, WellKnownLoadouts.Full)]
    public void GetLoadoutName_ReturnsExpectedLoadout(SubagentType type, string expected)
    {
        SubagentRunner.GetLoadoutName(type).Should().Be(expected);
    }

    [Fact]
    public void GetLoadoutName_UnknownValue_ReturnsMinimal()
    {
        SubagentRunner.GetLoadoutName((SubagentType)999).Should().Be(WellKnownLoadouts.Minimal);
    }

    [Fact]
    public void GetLoadoutName_AllDefinedValues_ReturnNonEmpty()
    {
        foreach (var type in Enum.GetValues<SubagentType>())
        {
            SubagentRunner.GetLoadoutName(type).Should().NotBeNullOrWhiteSpace(
                $"GetLoadoutName({type}) should return a non-empty loadout name");
        }
    }

    [Fact]
    public void GetLoadoutName_ExploreAndTask_ReturnDifferentLoadouts()
    {
        var explore = SubagentRunner.GetLoadoutName(SubagentType.Explore);
        var task = SubagentRunner.GetLoadoutName(SubagentType.Task);
        explore.Should().NotBe(task);
    }

    [Fact]
    public void GetLoadoutName_PlanAndReview_ReturnSameLoadout()
    {
        var plan = SubagentRunner.GetLoadoutName(SubagentType.Plan);
        var review = SubagentRunner.GetLoadoutName(SubagentType.Review);
        plan.Should().Be(review);
    }
}
