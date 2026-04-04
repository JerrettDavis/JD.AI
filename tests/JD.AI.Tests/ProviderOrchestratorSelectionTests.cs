using JD.AI.Core.Providers;
using JD.AI.Core.Routing;
using JD.AI.Startup;

namespace JD.AI.Tests;

public sealed class ProviderOrchestratorSelectionTests
{
    [Fact]
    public void EvaluateSelection_CliModelTakesHighestPrecedence()
    {
        var models = CreateModels();
        var opts = new CliOptions
        {
            CliModel = "sonnet",
            CliProvider = "claudecode",
            PrintMode = true,
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: "Ollama",
            defaultModel: "llama");

        Assert.True(decision.Handled);
        Assert.Null(decision.ErrorMessage);
        Assert.Equal("claude-sonnet", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_CliProviderUsesPromptWhenInteractive()
    {
        var models = CreateModels();
        var opts = new CliOptions { CliProvider = "ollama", PrintMode = false };
        var promptInvoked = false;

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null,
            promptSelector: candidates =>
            {
                promptInvoked = true;
                return candidates[^1];
            });

        Assert.True(promptInvoked);
        Assert.Equal("ollama-coder", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_DefaultModelPrecedesRouting()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = true };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: "claudecode",
            defaultModel: "claude-haiku");

        Assert.Equal("claude-haiku", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_DefaultProvider_NarrowsSelectionBeforePrompt()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = false };
        var promptInvoked = false;

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: "ollama",
            defaultModel: null,
            router: new NoopRouter(),
            promptSelector: candidates =>
            {
                promptInvoked = true;
                Assert.All(candidates, candidate => Assert.Equal("Ollama", candidate.ProviderName));
                return candidates[^1];
            });

        Assert.True(promptInvoked);
        Assert.Equal("ollama-coder", decision.SelectedModel?.Id);
    }

    [Fact]
    public void EvaluateSelection_RoutingSelectsLocalModelAndFallbacks()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = true, RoutingStrategy = "local-first" };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null,
            router: new DefaultModelRouter(),
            routingPolicy: RoutingPolicy.Default);

        Assert.Equal("ollama-chat", decision.SelectedModel?.Id);
        Assert.NotNull(decision.FallbackModelIds);
        Assert.Contains("claude-sonnet", decision.FallbackModelIds!);
    }


    [Fact]
    public void EvaluateSelection_RoutingDefaultsPreferToolCapableModels()
    {
        var models = new List<ProviderModelInfo>
        {
            new(
                "chat-only",
                "Chat Only",
                "Basic",
                ContextWindowTokens: 16_384,
                Capabilities: ModelCapabilities.Chat),
            new(
                "tool-capable",
                "Tool Capable",
                "Advanced",
                ContextWindowTokens: 128_000,
                Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null,
            router: new DefaultModelRouter(),
            routingPolicy: RoutingPolicy.Default);

        Assert.Equal("tool-capable", decision.SelectedModel?.Id);
        Assert.Equal("Advanced", decision.SelectedModel?.ProviderName);
    }
    [Fact]
    public void EvaluateSelection_CliModelNotFoundReturnsError()
    {
        var models = CreateModels();
        var opts = new CliOptions { CliModel = "does-not-exist" };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null);

        Assert.True(decision.Handled);
        Assert.Null(decision.SelectedModel);
        Assert.Contains("No model matching", decision.ErrorMessage);
    }

    [Fact]
    public void EvaluateSelection_InteractivePromptCancelled_ReturnsCancellationError()
    {
        var models = CreateModels();
        var opts = new CliOptions { PrintMode = false };

        var decision = ProviderOrchestrator.EvaluateSelection(
            opts,
            models,
            defaultProvider: null,
            defaultModel: null,
            router: new NoopRouter(),
            routingPolicy: RoutingPolicy.Default,
            promptSelector: _ => throw new OperationCanceledException());

        Assert.True(decision.Handled);
        Assert.Null(decision.SelectedModel);
        Assert.Equal("Model selection cancelled.", decision.ErrorMessage);
    }

    private static List<ProviderModelInfo> CreateModels() =>
    [
        new("claude-sonnet", "Claude Sonnet", "ClaudeCode", InputCostPerToken: 0.000003m, OutputCostPerToken: 0.000015m),
        new("claude-haiku", "Claude Haiku", "ClaudeCode", InputCostPerToken: 0.0000008m, OutputCostPerToken: 0.000004m),
        new("ollama-chat", "Ollama Chat", "Ollama", InputCostPerToken: 0m, OutputCostPerToken: 0m),
        new("ollama-coder", "Ollama Coder", "Ollama", InputCostPerToken: 0m, OutputCostPerToken: 0m),
    ];

    private sealed class NoopRouter : IModelRouter
    {
        public ModelRouteDecision Route(
            IReadOnlyList<ProviderModelInfo> candidates,
            RoutingPolicy policy) => ModelRouteDecision.None;
    }
}
