
using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Agents.Orchestration.Strategies;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Orchestration;
public class StrategyIntegrationTests
{
    private readonly ISubagentExecutor _executor = Substitute.For<ISubagentExecutor>();

    private static AgentResult MakeResult(string agentName, string output) => new()
    {
        AgentName = agentName,
        Output = output,
        Success = true,
    };

    private static SubagentConfig MakeAgent(string name, string prompt) => new()
    {
        Name = name,
        Prompt = prompt,
    };

    private AgentSession CreateDummySession()
    {
        var registry = Substitute.For<JD.AI.Core.Providers.IProviderRegistry>();
        var kernel = new Microsoft.SemanticKernel.Kernel();
        var model = new JD.AI.Core.Providers.ProviderModelInfo("test-model", "Test", "test");
        return new AgentSession(registry, kernel, model);
    }

    [Fact]
    public async Task SequentialStrategy_RunsInOrder_EachGetssPreviousOutput()
    {
        var agents = new[]
        {
            MakeAgent("explorer", "explore the code"),
            MakeAgent("planner", "make a plan"),
            MakeAgent("coder", "write the code"),
        };

        _executor.ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var config = callInfo.Arg<SubagentConfig>();
                return Task.FromResult(MakeResult(config.Name, $"output-of-{config.Name}"));
            });

        var strategy = new SequentialStrategy();
        var context = new TeamContext("test goal");
        var session = CreateDummySession();

        var result = await strategy.ExecuteAsync(agents, context, _executor, session);

        result.Strategy.Should().Be("sequential");
        result.Success.Should().BeTrue();
        result.AgentResults.Should().HaveCount(3);
        result.Output.Should().Be("output-of-coder");

        // Verify second and third agents receive augmented prompts with previous output
        await _executor.Received(3).ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>());

        // Verify scratchpad has outputs
        context.ReadScratchpad("output:explorer").Should().Be("output-of-explorer");
        context.ReadScratchpad("output:planner").Should().Be("output-of-planner");
        context.ReadScratchpad("output:coder").Should().Be("output-of-coder");
    }

    [Fact]
    public async Task FanOutStrategy_RunsInParallel_SynthesizerMerges()
    {
        var agents = new[]
        {
            MakeAgent("reviewer1", "review security"),
            MakeAgent("reviewer2", "review performance"),
            MakeAgent("reviewer3", "review style"),
        };

        _executor.ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var config = callInfo.Arg<SubagentConfig>();
                return Task.FromResult(MakeResult(config.Name, $"findings-from-{config.Name}"));
            });

        var strategy = new FanOutStrategy();
        var context = new TeamContext("multi-review");
        var session = CreateDummySession();

        var result = await strategy.ExecuteAsync(agents, context, _executor, session);

        result.Strategy.Should().Be("fan-out");
        result.Success.Should().BeTrue();
        // 3 workers + 1 synthesizer = 4 agent results
        result.AgentResults.Should().HaveCount(4);
        result.AgentResults.Should().ContainKey("synthesizer");

        // Executor should be called 4 times (3 parallel + 1 synthesizer)
        await _executor.Received(4).ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DebateStrategy_DebatersThenJudge()
    {
        var agents = new[]
        {
            new SubagentConfig { Name = "optimist", Prompt = "argue for", Perspective = "optimist" },
            new SubagentConfig { Name = "skeptic", Prompt = "argue against", Perspective = "skeptic" },
        };

        _executor.ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var config = callInfo.Arg<SubagentConfig>();
                return Task.FromResult(MakeResult(config.Name, $"argument-by-{config.Name}"));
            });

        var strategy = new DebateStrategy();
        var context = new TeamContext("should we migrate?");
        var session = CreateDummySession();

        var result = await strategy.ExecuteAsync(agents, context, _executor, session);

        result.Strategy.Should().Be("debate");
        result.Success.Should().BeTrue();
        // 2 debaters + 1 judge = 3 agent results
        result.AgentResults.Should().HaveCount(3);
        result.AgentResults.Should().ContainKey("judge");
        result.Output.Should().Be("argument-by-judge");

        // Verify events were recorded
        context.EventCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SupervisorStrategy_DispatchesToWorkers_ApprovedOnFirstReview()
    {
        var agents = new[]
        {
            MakeAgent("worker1", "do task A"),
            MakeAgent("worker2", "do task B"),
        };

        _executor.ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var config = callInfo.Arg<SubagentConfig>();
                var output = config.Name.StartsWith("supervisor", StringComparison.Ordinal)
                    ? "APPROVED: All work looks great, well done!"
                    : $"completed-{config.Name}";
                return Task.FromResult(MakeResult(config.Name, output));
            });

        var strategy = new SupervisorStrategy();
        var context = new TeamContext("build feature");
        var session = CreateDummySession();

        var result = await strategy.ExecuteAsync(agents, context, _executor, session);

        result.Strategy.Should().Be("supervisor");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("All work looks great, well done!");

        // 2 workers + 1 supervisor review = 3 calls
        await _executor.Received(3).ExecuteAsync(
            Arg.Any<SubagentConfig>(),
            Arg.Any<AgentSession>(),
            Arg.Any<TeamContext>(),
            Arg.Any<Action<SubagentProgress>?>(),
            Arg.Any<CancellationToken>());
    }
}
