using JD.AI.Core.Agents;
using JD.AI.Core.Agents.Orchestration;
using JD.AI.Core.Providers;
using Microsoft.SemanticKernel;
using NSubstitute;

namespace JD.AI.Tests.Agents.Orchestration;

/// <summary>
/// Shared test infrastructure for orchestration strategy tests.
/// </summary>
internal static class OrchestrationTestHelpers
{
    internal static SubagentConfig Cfg(
        string name,
        string prompt = "do work",
        string? perspective = null) =>
        new()
        {
            Name = name,
            Prompt = prompt,
            Perspective = perspective,
        };

    internal static AgentResult Success(
        string name,
        string output,
        long tokens = 100) =>
        new()
        {
            AgentName = name,
            Output = output,
            Success = true,
            TokensUsed = tokens,
            Duration = TimeSpan.FromMilliseconds(50),
        };

    internal static AgentResult Failure(
        string name,
        string error) =>
        new()
        {
            AgentName = name,
            Output = "",
            Success = false,
            Error = error,
            Duration = TimeSpan.FromMilliseconds(10),
        };

    internal static TeamContext Context(string goal = "test goal") =>
        new(goal);

    internal static AgentSession FakeSession()
    {
        var registry = Substitute.For<IProviderRegistry>();
        var kernel = Kernel.CreateBuilder().Build();
        var model = new ProviderModelInfo(
            "test-model", "Test Model", "TestProvider");
        return new AgentSession(registry, kernel, model);
    }
}

/// <summary>
/// Test double for <see cref="ISubagentExecutor"/> that records calls
/// and returns pre-configured results.
/// </summary>
internal sealed class FakeSubagentExecutor : ISubagentExecutor
{
    private readonly Dictionary<string, Func<SubagentConfig, AgentResult>>
        _resultFactories = new(StringComparer.Ordinal);

    private readonly List<SubagentConfig> _calls = [];

    /// <summary>All configs passed to ExecuteAsync, in call order.</summary>
    public IReadOnlyList<SubagentConfig> Calls => _calls;

    /// <summary>
    /// Register a fixed result for an agent name.
    /// </summary>
    public FakeSubagentExecutor WithResult(
        string agentName,
        AgentResult result)
    {
        _resultFactories[agentName] = _ => result;
        return this;
    }

    /// <summary>
    /// Register a result factory that receives the actual config
    /// (useful for name-pattern matching in supervisor retries).
    /// </summary>
    public FakeSubagentExecutor WithResultFactory(
        string namePrefix,
        Func<SubagentConfig, AgentResult> factory)
    {
        _resultFactories[namePrefix] = factory;
        return this;
    }

    public Task<AgentResult> ExecuteAsync(
        SubagentConfig config,
        AgentSession parentSession,
        TeamContext? teamContext = null,
        Action<SubagentProgress>? onProgress = null,
        CancellationToken ct = default)
    {
        _calls.Add(config);

        // Try exact name match first, then prefix match
        if (_resultFactories.TryGetValue(config.Name, out var factory))
            return Task.FromResult(factory(config));

        // Try prefix matching (for supervisor-review-1, etc.)
        foreach (var (prefix, f) in _resultFactories)
            if (config.Name.StartsWith(prefix, StringComparison.Ordinal))
                return Task.FromResult(f(config));

        // Default: success with generic output
        return Task.FromResult(
            OrchestrationTestHelpers.Success(
                config.Name,
                $"output from {config.Name}"));
    }
}
