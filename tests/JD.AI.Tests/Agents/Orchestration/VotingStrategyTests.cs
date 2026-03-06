using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class VotingStrategyTests
{
    private readonly VotingStrategy _sut = new();

    [Fact]
    public void Name_IsVoting() =>
        _sut.Name.Should().Be("voting");

    [Fact]
    public async Task ExecuteAsync_AllVotersPlusAggregator()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "approve"))
            .WithResult("v2", Success("v2", "approve"))
            .WithResult(
                "vote-aggregator",
                Success("vote-aggregator", "consensus: approve"));

        var result = await _sut.ExecuteAsync(
            [Cfg("v1"), Cfg("v2")],
            Context(),
            executor,
            FakeSession());

        executor.Calls.Should().HaveCount(3);
        result.Output.Should().Be("consensus: approve");
        result.Strategy.Should().Be("voting");
    }

    [Fact]
    public async Task ExecuteAsync_StoresVotesInScratchpad()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "yes"))
            .WithResult("v2", Success("v2", "no"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("v1"), Cfg("v2")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("vote:v1").Should().Be("yes");
        ctx.ReadScratchpad("vote:v2").Should().Be("no");
    }

    [Fact]
    public async Task ExecuteAsync_AggregatorReceivesAllVotes()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "opinion-A"))
            .WithResult("v2", Success("v2", "opinion-B"));

        await _sut.ExecuteAsync(
            [Cfg("v1"), Cfg("v2")],
            Context("decide"),
            executor,
            FakeSession());

        var aggCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "vote-aggregator", StringComparison.Ordinal));
        aggCall.Prompt.Should().Contain("opinion-A");
        aggCall.Prompt.Should().Contain("opinion-B");
        aggCall.Prompt.Should().Contain("decide");
    }

    [Fact]
    public async Task ExecuteAsync_FailedVoter_MarksNotSuccess()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "ok"))
            .WithResult("v2", Failure("v2", "error"))
            .WithResult(
                "vote-aggregator",
                Success("vote-aggregator", "partial"));

        var result = await _sut.ExecuteAsync(
            [Cfg("v1"), Cfg("v2")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_VotingMethodIncludedInPrompt()
    {
        var sut = new VotingStrategy { Method = VotingMethod.Unanimous };
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "yes"));

        await sut.ExecuteAsync(
            [Cfg("v1")],
            Context(),
            executor,
            FakeSession());

        var aggCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "vote-aggregator", StringComparison.Ordinal));
        aggCall.Prompt.Should().Contain("Unanimous");
    }

    [Fact]
    public async Task ExecuteAsync_WeightsIncludedInAggregationPrompt()
    {
        var sut = new VotingStrategy
        {
            Weights = new Dictionary<string, double>(StringComparer.Ordinal) { ["v1"] = 2.0 },
        };
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "vote"));

        await sut.ExecuteAsync(
            [Cfg("v1")],
            Context(),
            executor,
            FakeSession());

        var aggCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "vote-aggregator", StringComparison.Ordinal));
        aggCall.Prompt.Should().Contain("2.0");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultWeightApplied()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("v1", Success("v1", "vote"));

        await _sut.ExecuteAsync(
            [Cfg("v1")],
            Context(),
            executor,
            FakeSession());

        var aggCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "vote-aggregator", StringComparison.Ordinal));
        // Default weight is 1.0
        aggCall.Prompt.Should().Contain("weight=1.0");
    }
}
