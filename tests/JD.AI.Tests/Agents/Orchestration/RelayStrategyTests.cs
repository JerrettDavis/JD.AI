using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class RelayStrategyTests
{
    private readonly RelayStrategy _sut = new();

    [Fact]
    public void Name_IsRelay() =>
        _sut.Name.Should().Be("relay");

    [Fact]
    public async Task ExecuteAsync_ChainsOutputThroughAgents()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("r1", Success("r1", "draft"))
            .WithResult("r2", Success("r2", "polished"))
            .WithResult("r3", Success("r3", "final"));

        var result = await _sut.ExecuteAsync(
            [Cfg("r1"), Cfg("r2"), Cfg("r3")],
            Context("write docs"),
            executor,
            FakeSession());

        result.Output.Should().Be("final");
        result.Success.Should().BeTrue();
        result.Strategy.Should().Be("relay");
    }

    [Fact]
    public async Task ExecuteAsync_FirstAgentReceivesGoal()
    {
        var executor = new FakeSubagentExecutor();

        await _sut.ExecuteAsync(
            [Cfg("r1", perspective: "writer")],
            Context("write a poem"),
            executor,
            FakeSession());

        executor.Calls[0].Prompt.Should().Contain("write a poem");
    }

    [Fact]
    public async Task ExecuteAsync_SubsequentAgentsReceivePreviousOutput()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("r1", Success("r1", "first-pass"));

        await _sut.ExecuteAsync(
            [Cfg("r1"), Cfg("r2", perspective: "editor")],
            Context(),
            executor,
            FakeSession());

        var secondCall = executor.Calls[1];
        secondCall.Prompt.Should().Contain("first-pass");
        secondCall.Prompt.Should().Contain("editor");
    }

    [Fact]
    public async Task ExecuteAsync_StopEarly_OnNoChanges()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("r1", Success("r1", "good enough"))
            .WithResult("r2", Success("r2", "[NO_CHANGES]"))
            .WithResult("r3", Success("r3", "never reached"));

        var result = await _sut.ExecuteAsync(
            [Cfg("r1"), Cfg("r2"), Cfg("r3")],
            Context(),
            executor,
            FakeSession());

        // r3 should not have been called
        executor.Calls.Should().HaveCount(2);
        // Output stays as "good enough" because [NO_CHANGES] didn't update it
        result.Output.Should().Be("good enough");
    }

    [Fact]
    public async Task ExecuteAsync_StopEarlyDisabled_ContinuesOnNoChanges()
    {
        var sut = new RelayStrategy { StopEarly = false };
        var executor = new FakeSubagentExecutor()
            .WithResult("r1", Success("r1", "ok"))
            .WithResult("r2", Success("r2", "[NO_CHANGES]"))
            .WithResult("r3", Success("r3", "final"));

        await sut.ExecuteAsync(
            [Cfg("r1"), Cfg("r2"), Cfg("r3")],
            Context(),
            executor,
            FakeSession());

        executor.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExecuteAsync_StoresScratchpadPerRelay()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a", Success("a", "out-a"))
            .WithResult("b", Success("b", "out-b"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("a"), Cfg("b")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("relay:0:a").Should().Be("out-a");
        ctx.ReadScratchpad("relay:1:b").Should().Be("out-b");
    }

    [Fact]
    public async Task ExecuteAsync_FailedAgent_ReportsNotSuccess()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("r1", Success("r1", "ok"))
            .WithResult("r2", Failure("r2", "error"));

        var result = await _sut.ExecuteAsync(
            [Cfg("r1"), Cfg("r2")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SingleAgent_ReturnsDirectly()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("only", Success("only", "single output"));

        var result = await _sut.ExecuteAsync(
            [Cfg("only")],
            Context("input"),
            executor,
            FakeSession());

        result.Output.Should().Be("single output");
        executor.Calls.Should().HaveCount(1);
    }
}
