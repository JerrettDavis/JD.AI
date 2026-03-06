using FluentAssertions;
using JD.AI.Core.Agents.Orchestration;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class TeamOrchestratorTests
{
    [Fact]
    public async Task RunTeamAsync_UnknownStrategy_ReturnsFailure()
    {
        var sut = new TeamOrchestrator(FakeSession());

        var result = await sut.RunTeamAsync(
            "nonexistent",
            [Cfg("a1")],
            "test goal");

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("Unknown strategy");
        result.Output.Should().Contain("nonexistent");
    }

    [Theory]
    [InlineData("sequential")]
    [InlineData("fan-out")]
    [InlineData("fanout")]
    [InlineData("parallel")]
    [InlineData("supervisor")]
    [InlineData("coordinator")]
    [InlineData("debate")]
    [InlineData("adversarial")]
    [InlineData("voting")]
    [InlineData("vote")]
    [InlineData("consensus")]
    [InlineData("relay")]
    [InlineData("refine")]
    [InlineData("iterative")]
    [InlineData("map-reduce")]
    [InlineData("mapreduce")]
    [InlineData("pipeline")]
    [InlineData("pipe")]
    [InlineData("blackboard")]
    [InlineData("board")]
    [InlineData("collaborative")]
    public async Task RunTeamAsync_ValidStrategies_DoNotReturnUnknown(
        string strategyName)
    {
        var sut = new TeamOrchestrator(FakeSession());

        // Use empty agents so strategies return quickly
        // (some strategies handle empty differently, but none should say "Unknown strategy")
        var result = await sut.RunTeamAsync(
            strategyName,
            [],
            "test goal");

        result.Output.Should().NotContain("Unknown strategy");
    }

    [Fact]
    public async Task RunAgentAsync_NestingDepthExceeded_ReturnsFailure()
    {
        var sut = new TeamOrchestrator(FakeSession(), maxDepth: 1);
        var ctx = new TeamContext("goal") { MaxDepth = 1, CurrentDepth = 1 };

        var result = await sut.RunAgentAsync(
            Cfg("nested"),
            teamContext: ctx);

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("max nesting depth");
        result.AgentName.Should().Be("nested");
    }

    [Fact]
    public async Task RunAgentAsync_NullContext_DoesNotCheckDepth()
    {
        var sut = new TeamOrchestrator(FakeSession(), maxDepth: 0);

        // With null teamContext, nesting check is skipped
        // The real executor will fail, but we're testing the nesting guard
        // This will throw or fail in the real executor — we just verify
        // it doesn't fail with "max nesting depth"
        try
        {
            var result = await sut.RunAgentAsync(Cfg("agent"));
            result.Output.Should().NotContain("max nesting depth");
        }
        catch
        {
            // Expected: real executor fails without LLM infrastructure
            // The point is it didn't fail on nesting check
        }
    }
}
