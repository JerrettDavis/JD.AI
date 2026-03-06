using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class SequentialStrategyTests
{
    private readonly SequentialStrategy _sut = new();

    [Fact]
    public void Name_IsSequential() =>
        _sut.Name.Should().Be("sequential");

    [Fact]
    public async Task ExecuteAsync_SingleAgent_ReturnsItsOutput()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "hello"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("hello");
        result.Success.Should().BeTrue();
        result.Strategy.Should().Be("sequential");
        result.AgentResults.Should().ContainKey("a1");
    }

    [Fact]
    public async Task ExecuteAsync_TwoAgents_ChainsOutputInPrompt()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "step-one"))
            .WithResult("a2", Success("a2", "step-two"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1", "go"), Cfg("a2", "continue")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("step-two");

        // Second agent should have received augmented prompt
        var secondCall = executor.Calls[1];
        secondCall.Prompt.Should().Contain("step-one");
        secondCall.Prompt.Should()
            .Contain("--- Previous agent output ---");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyAgentList_ReturnsNoOutput()
    {
        var result = await _sut.ExecuteAsync(
            [],
            Context(),
            new FakeSubagentExecutor(),
            FakeSession());

        result.Output.Should().Be("(no output)");
        result.AgentResults.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_StoresResultsInScratchpad()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "val1"))
            .WithResult("a2", Success("a2", "val2"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("a1"), Cfg("a2")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("output:a1").Should().Be("val1");
        ctx.ReadScratchpad("output:a2").Should().Be("val2");
    }

    [Fact]
    public async Task ExecuteAsync_FailedAgent_ReportsNotSuccess()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "ok"))
            .WithResult("a2", Failure("a2", "boom"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1"), Cfg("a2")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FirstAgentGetsUnmodifiedPrompt()
    {
        var executor = new FakeSubagentExecutor();

        await _sut.ExecuteAsync(
            [Cfg("a1", "original prompt"), Cfg("a2")],
            Context(),
            executor,
            FakeSession());

        executor.Calls[0].Prompt.Should().Be("original prompt");
    }
}
