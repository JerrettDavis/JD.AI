using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class PipelineStrategyTests
{
    private readonly PipelineStrategy _sut = new();

    [Fact]
    public void Name_IsPipeline() =>
        _sut.Name.Should().Be("pipeline");

    [Fact]
    public async Task ExecuteAsync_ChainsInputThroughStages()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("stage1", Success("stage1", "parsed"))
            .WithResult("stage2", Success("stage2", "validated"))
            .WithResult("stage3", Success("stage3", "final"));

        var result = await _sut.ExecuteAsync(
            [Cfg("stage1"), Cfg("stage2"), Cfg("stage3")],
            Context("raw input"),
            executor,
            FakeSession());

        result.Output.Should().Be("final");
        result.Success.Should().BeTrue();

        // First stage should receive the goal as input
        executor.Calls[0].Prompt.Should().Contain("raw input");
        // Second stage should receive first stage's output
        executor.Calls[1].Prompt.Should().Contain("parsed");
        // Third stage should receive second stage's output
        executor.Calls[2].Prompt.Should().Contain("validated");
    }

    [Fact]
    public async Task ExecuteAsync_FailFast_StopsOnFirstFailure()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("s1", Success("s1", "ok"))
            .WithResult("s2", Failure("s2", "error"))
            .WithResult("s3", Success("s3", "never reached"));

        var result = await _sut.ExecuteAsync(
            [Cfg("s1"), Cfg("s2"), Cfg("s3")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("Pipeline failed");
        result.Output.Should().Contain("s2");

        // Third stage should NOT have been called
        executor.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_SingleStage_ReturnsDirectly()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("only", Success("only", "done"));

        var result = await _sut.ExecuteAsync(
            [Cfg("only")],
            Context("input"),
            executor,
            FakeSession());

        result.Output.Should().Be("done");
        result.Success.Should().BeTrue();
        executor.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_StoresScratchpadPerStage()
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

        ctx.ReadScratchpad("pipeline:0:a").Should().Be("out-a");
        ctx.ReadScratchpad("pipeline:1:b").Should().Be("out-b");
    }

    [Fact]
    public async Task ExecuteAsync_PromptContainsStageMetadata()
    {
        var executor = new FakeSubagentExecutor();

        await _sut.ExecuteAsync(
            [
                Cfg("parse", perspective: "parser"),
                Cfg("validate", perspective: "validator"),
            ],
            Context("data"),
            executor,
            FakeSession());

        var firstPrompt = executor.Calls[0].Prompt;
        firstPrompt.Should().Contain("stage 1 of 2");
        firstPrompt.Should().Contain("parser");

        var secondPrompt = executor.Calls[1].Prompt;
        secondPrompt.Should().Contain("stage 2 of 2");
        secondPrompt.Should().Contain("validator");
    }

    [Fact]
    public async Task ExecuteAsync_RecordsEventsPerStage()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("a", Success("a", "ok"))
            .WithResult("b", Success("b", "ok"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("a"), Cfg("b")],
            ctx,
            executor,
            FakeSession());

        ctx.EventCount.Should().BeGreaterThanOrEqualTo(2);
    }
}
