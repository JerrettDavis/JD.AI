using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class FanOutStrategyTests
{
    private readonly FanOutStrategy _sut = new();

    [Fact]
    public void Name_IsFanOut() =>
        _sut.Name.Should().Be("fan-out");

    [Fact]
    public async Task ExecuteAsync_AllAgentsExecuted()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "result-1"))
            .WithResult("a2", Success("a2", "result-2"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1"), Cfg("a2")],
            Context(),
            executor,
            FakeSession());

        // All agents plus synthesizer should have been called
        executor.Calls.Should().HaveCount(3);
        executor.Calls.Select(c => c.Name)
            .Should().Contain("a1")
            .And.Contain("a2")
            .And.Contain("synthesizer");
    }

    [Fact]
    public async Task ExecuteAsync_SynthesizerReceivesAllOutputs()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("r1", Success("r1", "review-one"))
            .WithResult("r2", Success("r2", "review-two"))
            .WithResult(
                "synthesizer",
                Success("synthesizer", "combined"));

        await _sut.ExecuteAsync(
            [Cfg("r1"), Cfg("r2")],
            Context("review this"),
            executor,
            FakeSession());

        var synthCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "synthesizer", StringComparison.Ordinal));
        synthCall.Prompt.Should().Contain("review-one");
        synthCall.Prompt.Should().Contain("review-two");
        synthCall.Prompt.Should().Contain("review this");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSynthesizerOutput()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "partial"))
            .WithResult(
                "synthesizer",
                Success("synthesizer", "final synthesis"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("final synthesis");
    }

    [Fact]
    public async Task ExecuteAsync_StoresOutputsInScratchpad()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("x", Success("x", "val-x"))
            .WithResult("y", Success("y", "val-y"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("x"), Cfg("y")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("output:x").Should().Be("val-x");
        ctx.ReadScratchpad("output:y").Should().Be("val-y");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessBasedOnAgents_NotSynthesizer()
    {
        // All agents succeed but synthesizer fails — still success
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Success("a1", "ok"))
            .WithResult(
                "synthesizer",
                Failure("synthesizer", "synth error"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1")],
            Context(),
            executor,
            FakeSession());

        // Success is based on agent results, not synthesizer
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FailedAgent_MarksNotSuccess()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a1", Failure("a1", "boom"))
            .WithResult(
                "synthesizer",
                Success("synthesizer", "partial"));

        var result = await _sut.ExecuteAsync(
            [Cfg("a1")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }
}
