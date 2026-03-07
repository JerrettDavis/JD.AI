using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class DebateStrategyTests
{
    private readonly DebateStrategy _sut = new();

    [Fact]
    public void Name_IsDebate() =>
        _sut.Name.Should().Be("debate");

    [Fact]
    public async Task ExecuteAsync_AllDebatersPlusJudge()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("optimist", Success("optimist", "it will work"))
            .WithResult("skeptic", Success("skeptic", "it might fail"))
            .WithResult(
                "judge",
                Success("judge", "balanced: proceed with caution"));

        var result = await _sut.ExecuteAsync(
            [
                Cfg("optimist", perspective: "optimist"),
                Cfg("skeptic", perspective: "skeptic"),
            ],
            Context("should we ship?"),
            executor,
            FakeSession());

        executor.Calls.Should().HaveCount(3);
        result.Output.Should().Be("balanced: proceed with caution");
        result.Strategy.Should().Be("debate");
    }

    [Fact]
    public async Task ExecuteAsync_JudgeReceivesAllArguments()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("d1", Success("d1", "argument-alpha"))
            .WithResult("d2", Success("d2", "argument-beta"));

        await _sut.ExecuteAsync(
            [Cfg("d1"), Cfg("d2")],
            Context("evaluate risk"),
            executor,
            FakeSession());

        var judgeCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "judge", StringComparison.Ordinal));
        judgeCall.Prompt.Should().Contain("argument-alpha");
        judgeCall.Prompt.Should().Contain("argument-beta");
        judgeCall.Prompt.Should().Contain("evaluate risk");
    }

    [Fact]
    public async Task ExecuteAsync_StoresArgumentsInScratchpad()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("d1", Success("d1", "arg-1"))
            .WithResult("d2", Success("d2", "arg-2"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("d1"), Cfg("d2")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("argument:d1").Should().Be("arg-1");
        ctx.ReadScratchpad("argument:d2").Should().Be("arg-2");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessBasedOnJudge()
    {
        // Debaters succeed but judge fails — result is not success
        var executor = new FakeSubagentExecutor()
            .WithResult("d1", Success("d1", "ok"))
            .WithResult("judge", Failure("judge", "judge error"));

        var result = await _sut.ExecuteAsync(
            [Cfg("d1")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FailedDebaterDoesNotBlockJudge()
    {
        // Debater fails but judge still runs and succeeds
        var executor = new FakeSubagentExecutor()
            .WithResult("d1", Failure("d1", "error"))
            .WithResult("judge", Success("judge", "partial ruling"));

        var result = await _sut.ExecuteAsync(
            [Cfg("d1")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeTrue();
        result.Output.Should().Be("partial ruling");
    }

    [Fact]
    public async Task ExecuteAsync_PerspectiveUsedInDebaterPrompt()
    {
        var executor = new FakeSubagentExecutor();

        await _sut.ExecuteAsync(
            [Cfg("d1", perspective: "the pragmatist")],
            Context(),
            executor,
            FakeSession());

        // First call is the debater (before judge)
        var debaterCall = executor.Calls[0];
        debaterCall.Perspective.Should().Be("the pragmatist");
    }
}
