using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class SupervisorStrategyTests
{
    private readonly SupervisorStrategy _sut = new();

    [Fact]
    public void Name_IsSupervisor() =>
        _sut.Name.Should().Be("supervisor");

    [Fact]
    public async Task ExecuteAsync_ApprovedOnFirstReview()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Success("w1", "analysis-1"))
            .WithResult("w2", Success("w2", "analysis-2"))
            .WithResult(
                "supervisor-review-0",
                Success("supervisor-review-0", "APPROVED: looks great"));

        var result = await _sut.ExecuteAsync(
            [Cfg("w1"), Cfg("w2")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("looks great");
        result.Success.Should().BeTrue();
        result.Strategy.Should().Be("supervisor");
    }

    [Fact]
    public async Task ExecuteAsync_RedirectRerunsWorkersThenApproves()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Success("w1", "draft-1"))
            .WithResult(
                "supervisor-review-0",
                Success("supervisor-review-0", "REDIRECT: needs more detail"))
            .WithResult(
                "supervisor-review-1",
                Success("supervisor-review-1", "APPROVED: detailed result"));

        var result = await _sut.ExecuteAsync(
            [Cfg("w1")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("detailed result");

        // Should have: w1, supervisor-review-0, w1-retry-0, supervisor-review-1
        executor.Calls.Should().HaveCount(4);
        executor.Calls.Select(c => c.Name)
            .Should().Contain("w1-retry-0");
    }

    [Fact]
    public async Task ExecuteAsync_RetryPromptContainsFeedback()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Success("w1", "draft"))
            .WithResult(
                "supervisor-review-0",
                Success("supervisor-review-0", "REDIRECT: add error handling"))
            .WithResult(
                "supervisor-review-1",
                Success("supervisor-review-1", "APPROVED: done"));

        await _sut.ExecuteAsync(
            [Cfg("w1", "original task")],
            Context(),
            executor,
            FakeSession());

        var retryCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "w1-retry-0", StringComparison.Ordinal));
        retryCall.Prompt.Should().Contain("add error handling");
        retryCall.Prompt.Should().Contain("draft");
    }

    [Fact]
    public async Task ExecuteAsync_NeitherApprovedNorRedirect_TreatsAsFinalOutput()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Success("w1", "done"))
            .WithResult(
                "supervisor-review-0",
                Success(
                    "supervisor-review-0",
                    "Final thoughts: all good"));

        var result = await _sut.ExecuteAsync(
            [Cfg("w1")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("Final thoughts: all good");
    }

    [Fact]
    public async Task ExecuteAsync_StoresWorkerOutputsInScratchpad()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Success("w1", "out-1"))
            .WithResult("w2", Success("w2", "out-2"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("w1"), Cfg("w2")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("output:w1").Should().Be("out-1");
        ctx.ReadScratchpad("output:w2").Should().Be("out-2");
    }

    [Fact]
    public async Task ExecuteAsync_ReviewPromptContainsWorkerOutputs()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Success("w1", "my-analysis"));

        await _sut.ExecuteAsync(
            [Cfg("w1")],
            Context("review code"),
            executor,
            FakeSession());

        var reviewCall = executor.Calls.First(
            c => c.Name.StartsWith(
                "supervisor-review", StringComparison.Ordinal));
        reviewCall.Prompt.Should().Contain("my-analysis");
        reviewCall.Prompt.Should().Contain("review code");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessRequiresAnySuccessfulResult()
    {
        // Worker fails but supervisor review succeeds — still success
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Failure("w1", "error"))
            .WithResult(
                "supervisor-review-0",
                Success(
                    "supervisor-review-0",
                    "APPROVED: partial ok"));

        var result = await _sut.ExecuteAsync(
            [Cfg("w1")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_AllFailures_ReportsNotSuccess()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("w1", Failure("w1", "error"))
            .WithResultFactory(
                "supervisor-review",
                cfg => Failure(cfg.Name, "review failed"));

        var result = await _sut.ExecuteAsync(
            [Cfg("w1")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }
}
