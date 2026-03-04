using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Agents.Orchestration.Strategies;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Orchestration;

public sealed class VotingStrategyTests
{
    private readonly ISubagentExecutor _executor = Substitute.For<ISubagentExecutor>();
    private readonly AgentSession _session;

    public VotingStrategyTests()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "TestProvider");
        _session = new AgentSession(registry, kernel, model);
    }

    [Fact]
    public void Name_ReturnsVoting()
    {
        var strategy = new VotingStrategy();
        Assert.Equal("voting", strategy.Name);
    }

    [Fact]
    public async Task ExecuteAsync_RunsAllAgentsInParallel()
    {
        var strategy = new VotingStrategy();
        var context = new TeamContext("Review this code");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "reviewer-1", Prompt = "review" },
            new() { Name = "reviewer-2", Prompt = "review" },
            new() { Name = "reviewer-3", Prompt = "review" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.Arg<SubagentConfig>().Name;
                return new AgentResult
                {
                    AgentName = name,
                    Output = $"Result from {name}",
                    Success = true,
                };
            });

        var result = await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.True(result.Success);
        Assert.Equal("voting", result.Strategy);
        // 3 agents + 1 aggregator = 4 results
        Assert.Equal(4, result.AgentResults.Count);
        Assert.Contains("reviewer-1", result.AgentResults.Keys);
        Assert.Contains("reviewer-2", result.AgentResults.Keys);
        Assert.Contains("reviewer-3", result.AgentResults.Keys);
        Assert.Contains("vote-aggregator", result.AgentResults.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_WritesVotesToScratchpad()
    {
        var strategy = new VotingStrategy();
        var context = new TeamContext("Classify this");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "agent-a", Prompt = "classify" },
            new() { Name = "agent-b", Prompt = "classify" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.Arg<SubagentConfig>().Name;
                return new AgentResult
                {
                    AgentName = name,
                    Output = $"Vote: {name}",
                    Success = true,
                };
            });

        await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.Equal("Vote: agent-a", context.ReadScratchpad("vote:agent-a"));
        Assert.Equal("Vote: agent-b", context.ReadScratchpad("vote:agent-b"));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsFailureWhenAgentFails()
    {
        var strategy = new VotingStrategy();
        var context = new TeamContext("test");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "ok-agent", Prompt = "work" },
            new() { Name = "fail-agent", Prompt = "work" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.Arg<SubagentConfig>().Name;
                return new AgentResult
                {
                    AgentName = name,
                    Output = $"Result from {name}",
                    Success = !string.Equals(name, "fail-agent", StringComparison.Ordinal),
                };
            });

        var result = await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.False(result.Success);
    }

    [Fact]
    public void DefaultVotingMethod_IsMajority()
    {
        var strategy = new VotingStrategy();
        Assert.Equal(VotingMethod.Majority, strategy.Method);
    }

    [Fact]
    public void Weights_DefaultsToNull()
    {
        var strategy = new VotingStrategy();
        Assert.Null(strategy.Weights);
    }
}

public sealed class RelayStrategyTests
{
    private readonly ISubagentExecutor _executor = Substitute.For<ISubagentExecutor>();
    private readonly AgentSession _session;

    public RelayStrategyTests()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "TestProvider");
        _session = new AgentSession(registry, kernel, model);
    }

    [Fact]
    public void Name_ReturnsRelay()
    {
        var strategy = new RelayStrategy();
        Assert.Equal("relay", strategy.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ChainsOutputBetweenAgents()
    {
        var strategy = new RelayStrategy();
        var context = new TeamContext("Write a report");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "drafter", Prompt = "draft", Perspective = "content completeness" },
            new() { Name = "editor", Prompt = "edit", Perspective = "clarity" },
            new() { Name = "reviewer", Prompt = "review", Perspective = "accuracy" },
        };

        var callOrder = new List<string>();

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.Arg<SubagentConfig>().Name;
                callOrder.Add(name);
                return new AgentResult
                {
                    AgentName = name,
                    Output = $"Refined by {name}",
                    Success = true,
                };
            });

        var result = await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.Equal("relay", result.Strategy);
        Assert.True(result.Success);
        // Output should be from the last agent
        Assert.Equal("Refined by reviewer", result.Output);
        // Agents should execute in order
        Assert.Equal(["drafter", "editor", "reviewer"], callOrder);
    }

    [Fact]
    public async Task ExecuteAsync_StopsEarlyOnNoChanges()
    {
        var strategy = new RelayStrategy { StopEarly = true };
        var context = new TeamContext("Perfect this");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "first", Prompt = "improve" },
            new() { Name = "second", Prompt = "improve" },
            new() { Name = "third", Prompt = "improve" },
        };

        var callCount = 0;

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                var name = callInfo.Arg<SubagentConfig>().Name;
                var output = callCount == 2 ? "[NO_CHANGES]" : $"Improved by {name}";
                return new AgentResult
                {
                    AgentName = name,
                    Output = output,
                    Success = true,
                };
            });

        var result = await strategy.ExecuteAsync(agents, context, _executor, _session);

        // Should have stopped after agent 2 returned [NO_CHANGES]
        Assert.Equal(2, callCount);
        Assert.Equal(2, result.AgentResults.Count);
        Assert.DoesNotContain("third", result.AgentResults.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_WritesRelayResultsToScratchpad()
    {
        var strategy = new RelayStrategy();
        var context = new TeamContext("Build it");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "builder", Prompt = "build" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResult
            {
                AgentName = "builder",
                Output = "Built result",
                Success = true,
            });

        await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.Equal("Built result", context.ReadScratchpad("relay:0:builder"));
    }
}

public sealed class MapReduceStrategyTests
{
    private readonly ISubagentExecutor _executor = Substitute.For<ISubagentExecutor>();
    private readonly AgentSession _session;

    public MapReduceStrategyTests()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo("test", "Test", "TestProvider");
        _session = new AgentSession(registry, kernel, model);
    }

    [Fact]
    public void Name_ReturnsMapReduce()
    {
        var strategy = new MapReduceStrategy();
        Assert.Equal("map-reduce", strategy.Name);
    }

    [Fact]
    public async Task ExecuteAsync_RunsMappersAndReducer()
    {
        var strategy = new MapReduceStrategy();
        var context = new TeamContext("Analyze files");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "mapper-1", Prompt = "analyze src/" },
            new() { Name = "mapper-2", Prompt = "analyze tests/" },
            new() { Name = "reducer", Prompt = "merge results" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.Arg<SubagentConfig>().Name;
                return new AgentResult
                {
                    AgentName = name,
                    Output = $"Output from {name}",
                    Success = true,
                };
            });

        var result = await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.True(result.Success);
        Assert.Equal("map-reduce", result.Strategy);
        Assert.Equal(3, result.AgentResults.Count);
        Assert.Contains("mapper-1", result.AgentResults.Keys);
        Assert.Contains("mapper-2", result.AgentResults.Keys);
        Assert.Contains("reducer", result.AgentResults.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_NoAgents_ReturnsFailed()
    {
        var strategy = new MapReduceStrategy();
        var context = new TeamContext("nothing to do");

        var result = await strategy.ExecuteAsync([], context, _executor, _session);

        Assert.False(result.Success);
        Assert.Contains("No agents", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_SingleAgent_ActsAsMapper()
    {
        var strategy = new MapReduceStrategy();
        var context = new TeamContext("Quick scan");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "solo", Prompt = "scan" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResult
            {
                AgentName = "solo",
                Output = "Scanned everything",
                Success = true,
            });

        var result = await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.True(result.Success);
        // Single agent = mapper only, output is concatenation
        Assert.Contains("solo", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WritesMapResultsToScratchpad()
    {
        var strategy = new MapReduceStrategy();
        var context = new TeamContext("Analyze");

        var agents = new List<SubagentConfig>
        {
            new() { Name = "m1", Prompt = "analyze chunk 1" },
            new() { Name = "reducer", Prompt = "merge" },
        };

        _executor.ExecuteAsync(Arg.Any<SubagentConfig>(), Arg.Any<AgentSession>(),
                Arg.Any<TeamContext>(), Arg.Any<Action<SubagentProgress>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.Arg<SubagentConfig>().Name;
                return new AgentResult
                {
                    AgentName = name,
                    Output = $"Result from {name}",
                    Success = true,
                };
            });

        await strategy.ExecuteAsync(agents, context, _executor, _session);

        Assert.Equal("Result from m1", context.ReadScratchpad("map:m1"));
    }

    [Fact]
    public void MaxParallelism_DefaultsToFour()
    {
        var strategy = new MapReduceStrategy();
        Assert.Equal(4, strategy.MaxParallelism);
    }
}
