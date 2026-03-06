using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class BlackboardStrategyTests
{
    private readonly BlackboardStrategy _sut = new();

    [Fact]
    public void Name_IsBlackboard() =>
        _sut.Name.Should().Be("blackboard");

    [Fact]
    public async Task ExecuteAsync_ImmediateConvergence_StopsAfterOneRound()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "[CONVERGED]"))
            .WithResult("a2", Success("a2", "[CONVERGED]"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1"), Cfg("a2")],
            Context("analyze"),
            executor,
            FakeSession());

        // Only one round (2 agents called once each)
        executor.Calls.Should().HaveCount(2);
        result.Success.Should().BeTrue();
        result.Strategy.Should().Be("blackboard");
    }

    [Fact]
    public async Task ExecuteAsync_ContributionsUpdateBlackboardState()
    {
        var sut = new BlackboardStrategy { MaxIterations = 1 };
        var executor = new FakeSubagentExecutor()
            .WithResult(
                "analyst",
                Success("analyst", "finding: X is broken"));

        var ctx = Context("investigate");

        await sut.ExecuteAsync(
            [Cfg("analyst", perspective: "analyst")],
            ctx,
            executor,
            FakeSession());

        var state = ctx.ReadScratchpad("blackboard:state");
        state.Should().Contain("finding: X is broken");
        state.Should().Contain("analyst");
    }

    [Fact]
    public async Task ExecuteAsync_MaxIterationsRespected()
    {
        var sut = new BlackboardStrategy { MaxIterations = 2 };
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "new info"));

        await sut.ExecuteAsync(
            [Cfg("a1")],
            Context(),
            executor,
            FakeSession());

        // 2 iterations × 1 agent = 2 calls
        executor.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_StoresScratchpadPerIteration()
    {
        var sut = new BlackboardStrategy { MaxIterations = 2 };
        var executor = new FakeSubagentExecutor()
            .WithResult("x", Success("x", "data-x"));

        var ctx = Context();

        await sut.ExecuteAsync(
            [Cfg("x")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("blackboard:0:x").Should().Be("data-x");
        ctx.ReadScratchpad("blackboard:1:x").Should().Be("data-x");
    }

    [Fact]
    public async Task ExecuteAsync_FailedAgent_ReportsNotSuccess()
    {
        var sut = new BlackboardStrategy { MaxIterations = 1 };
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Failure("a1", "error"));

        var result = await sut.ExecuteAsync(
            [Cfg("a1")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_OutputIsFinalBlackboardState()
    {
        var sut = new BlackboardStrategy { MaxIterations = 1 };
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "contribution"));

        var result = await sut.ExecuteAsync(
            [Cfg("a1", perspective: "specialist")],
            Context("goal"),
            executor,
            FakeSession());

        result.Output.Should().Contain("goal");
        result.Output.Should().Contain("contribution");
    }

    [Fact]
    public async Task ExecuteAsync_PromptContainsRoleAndGoal()
    {
        var sut = new BlackboardStrategy { MaxIterations = 1 };
        var executor = new FakeSubagentExecutor();

        await sut.ExecuteAsync(
            [Cfg("a1", perspective: "detective")],
            Context("solve mystery"),
            executor,
            FakeSession());

        var prompt = executor.Calls[0].Prompt;
        prompt.Should().Contain("detective");
        prompt.Should().Contain("solve mystery");
    }

    [Fact]
    public async Task ExecuteAsync_InitialBlackboardStateIsGoal()
    {
        var sut = new BlackboardStrategy { MaxIterations = 1 };
        var executor = new FakeSubagentExecutor();

        var ctx = Context("the goal");

        await sut.ExecuteAsync(
            [Cfg("a1")],
            ctx,
            executor,
            FakeSession());

        // First agent's prompt should contain the goal as blackboard state
        executor.Calls[0].Prompt.Should().Contain("the goal");
    }
}
