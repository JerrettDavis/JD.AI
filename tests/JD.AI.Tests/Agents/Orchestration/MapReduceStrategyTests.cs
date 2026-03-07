using FluentAssertions;
using JD.AI.Core.Agents.Orchestration.Strategies;
using static JD.AI.Tests.Agents.Orchestration.OrchestrationTestHelpers;

namespace JD.AI.Tests.Agents.Orchestration;

public sealed class MapReduceStrategyTests
{
    private readonly MapReduceStrategy _sut = new();

    [Fact]
    public void Name_IsMapReduce() =>
        _sut.Name.Should().Be("map-reduce");

    [Fact]
    public async Task ExecuteAsync_EmptyAgents_ReturnsFailure()
    {
        var result = await _sut.ExecuteAsync(
            [],
            Context(),
            new FakeSubagentExecutor(),
            FakeSession());

        result.Success.Should().BeFalse();
        result.Output.Should().Contain("No agents configured");
    }

    [Fact]
    public async Task ExecuteAsync_MappersAndReducer()
    {
        // First two are mappers, last is reducer
        var executor = new FakeSubagentExecutor()
            .WithResult("m1", Success("m1", "chunk-1"))
            .WithResult("m2", Success("m2", "chunk-2"))
            .WithResult("reducer", Success("reducer", "merged"));

        var result = await _sut.ExecuteAsync(
            [Cfg("m1"), Cfg("m2"), Cfg("reducer")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Be("merged");
        result.Strategy.Should().Be("map-reduce");
    }

    [Fact]
    public async Task ExecuteAsync_ReducerReceivesAllMapperOutputs()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("m1", Success("m1", "data-alpha"))
            .WithResult("m2", Success("m2", "data-beta"));

        await _sut.ExecuteAsync(
            [Cfg("m1"), Cfg("m2"), Cfg("reducer")],
            Context("analyze logs"),
            executor,
            FakeSession());

        var reducerCall = executor.Calls.First(
            c => string.Equals(
                c.Name, "reducer", StringComparison.Ordinal));
        reducerCall.Prompt.Should().Contain("data-alpha");
        reducerCall.Prompt.Should().Contain("data-beta");
        reducerCall.Prompt.Should().Contain("analyze logs");
    }

    [Fact]
    public async Task ExecuteAsync_SingleAgent_IsMapperWithConcatenation()
    {
        // With only one agent, it's a mapper with no separate reducer
        var executor = new FakeSubagentExecutor()
            .WithResult("solo", Success("solo", "single result"));

        var result = await _sut.ExecuteAsync(
            [Cfg("solo")],
            Context(),
            executor,
            FakeSession());

        result.Output.Should().Contain("single result");
        executor.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_StoresMapOutputsInScratchpad()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("m1", Success("m1", "val-1"))
            .WithResult("m2", Success("m2", "val-2"));

        var ctx = Context();

        await _sut.ExecuteAsync(
            [Cfg("m1"), Cfg("m2"), Cfg("reducer")],
            ctx,
            executor,
            FakeSession());

        ctx.ReadScratchpad("map:m1").Should().Be("val-1");
        ctx.ReadScratchpad("map:m2").Should().Be("val-2");
    }

    [Fact]
    public async Task ExecuteAsync_SuccessBasedOnMappers_NotReducer()
    {
        // Mappers succeed but reducer fails — still success
        var executor = new FakeSubagentExecutor()
            .WithResult("m1", Success("m1", "ok"))
            .WithResult("reducer", Failure("reducer", "error"));

        var result = await _sut.ExecuteAsync(
            [Cfg("m1"), Cfg("reducer")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FailedMapper_ReportsNotSuccess()
    {
        var executor = new FakeSubagentExecutor()
            .WithResult("m1", Failure("m1", "error"))
            .WithResult("reducer", Success("reducer", "partial"));

        var result = await _sut.ExecuteAsync(
            [Cfg("m1"), Cfg("reducer")],
            Context(),
            executor,
            FakeSession());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_BoundedParallelism()
    {
        var sut = new MapReduceStrategy { MaxParallelism = 2 };
        var executor = new FakeSubagentExecutor()
            .WithResult("m1", Success("m1", "a"))
            .WithResult("m2", Success("m2", "b"))
            .WithResult("m3", Success("m3", "c"));

        var result = await sut.ExecuteAsync(
            [Cfg("m1"), Cfg("m2"), Cfg("m3"), Cfg("reducer")],
            Context(),
            executor,
            FakeSession());

        // All mappers should still execute, just with bounded concurrency
        executor.Calls.Should().HaveCount(4);
        result.Success.Should().BeTrue();
    }
}
