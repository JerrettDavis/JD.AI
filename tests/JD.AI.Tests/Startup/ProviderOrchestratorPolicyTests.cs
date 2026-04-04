using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Routing;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class ProviderOrchestratorPolicyTests
{
    // ── ParseRoutingStrategy ─────────────────────────────────────────────

    [Theory]
    [InlineData("local-first", RoutingStrategy.LocalFirst)]
    [InlineData("local", RoutingStrategy.LocalFirst)]
    [InlineData("cost", RoutingStrategy.CostOptimized)]
    [InlineData("cost-optimized", RoutingStrategy.CostOptimized)]
    [InlineData("capability", RoutingStrategy.CapabilityDriven)]
    [InlineData("capability-driven", RoutingStrategy.CapabilityDriven)]
    [InlineData("latency", RoutingStrategy.LatencyOptimized)]
    [InlineData("latency-optimized", RoutingStrategy.LatencyOptimized)]
    public void ParseRoutingStrategy_KnownValues(string input, RoutingStrategy expected) =>
        ProviderOrchestrator.ParseRoutingStrategy(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("random-garbage")]
    public void ParseRoutingStrategy_UnknownDefaults_ToLocalFirst(string? input) =>
        ProviderOrchestrator.ParseRoutingStrategy(input).Should().Be(RoutingStrategy.LocalFirst);

    [Fact]
    public void ParseRoutingStrategy_CaseInsensitive() =>
        ProviderOrchestrator.ParseRoutingStrategy("LOCAL-FIRST").Should().Be(RoutingStrategy.LocalFirst);

    [Fact]
    public void ParseRoutingStrategy_TrimsWhitespace() =>
        ProviderOrchestrator.ParseRoutingStrategy("  cost  ").Should().Be(RoutingStrategy.CostOptimized);

    // ── ParseRoutingCapabilities ─────────────────────────────────────────

    [Fact]
    public void ParseRoutingCapabilities_Empty_DefaultsToChatAndTools() =>
        ProviderOrchestrator.ParseRoutingCapabilities([])
            .Should().Be(ModelCapabilities.Chat | ModelCapabilities.ToolCalling);

    [Fact]
    public void ParseRoutingCapabilities_Chat()
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities(["chat"]);
        caps.Should().HaveFlag(ModelCapabilities.Chat);
    }

    [Theory]
    [InlineData("tools")]
    [InlineData("tool-calling")]
    [InlineData("toolcalling")]
    [InlineData("json")]
    public void ParseRoutingCapabilities_ToolCallingAliases(string input)
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities([input]);
        caps.Should().HaveFlag(ModelCapabilities.ToolCalling);
    }

    [Fact]
    public void ParseRoutingCapabilities_Vision()
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities(["vision"]);
        caps.Should().HaveFlag(ModelCapabilities.Vision);
    }

    [Fact]
    public void ParseRoutingCapabilities_Embeddings()
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities(["embeddings"]);
        caps.Should().HaveFlag(ModelCapabilities.Embeddings);
    }

    [Fact]
    public void ParseRoutingCapabilities_MultipleCombined()
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities(["chat", "vision", "tools"]);
        caps.Should().HaveFlag(ModelCapabilities.Chat);
        caps.Should().HaveFlag(ModelCapabilities.Vision);
        caps.Should().HaveFlag(ModelCapabilities.ToolCalling);
    }

    [Fact]
    public void ParseRoutingCapabilities_UnknownOnly_DefaultsToChatAndTools()
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities(["unknown-thing"]);
        caps.Should().Be(ModelCapabilities.Chat | ModelCapabilities.ToolCalling);
    }

    [Fact]
    public void ParseRoutingCapabilities_CaseInsensitive()
    {
        var caps = ProviderOrchestrator.ParseRoutingCapabilities(["VISION", "CHAT"]);
        caps.Should().HaveFlag(ModelCapabilities.Vision);
        caps.Should().HaveFlag(ModelCapabilities.Chat);
    }

    // ── EvaluateSelection edge cases ─────────────────────────────────────

    [Fact]
    public void EvaluateSelection_NoModels_PrintMode_ReturnsError()
    {
        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            [],
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("No models available");
    }

    [Fact]
    public void EvaluateSelection_NoModels_InteractiveMode_ReturnsError()
    {
        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            [],
            defaultProvider: null,
            defaultModel: null,
            promptSelector: _ => throw new InvalidOperationException("Should not prompt"));

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("No models available");
    }

    [Fact]
    public void EvaluateSelection_SingleModel_PrintMode_SelectsIt()
    {
        var models = new List<ProviderModelInfo>
        {
            new("only-model", "Only", "TestProvider"),
        };
        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("only-model");
    }

    [Fact]
    public void EvaluateSelection_CliProviderNotFound_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "ProviderA"),
        };
        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliProvider = "nonexistent", PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("No models from provider");
    }

    [Fact]
    public void EvaluateSelection_CliProviderSingleMatch_PrintMode_SelectsIt()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "Ollama"),
            new("m2", "Model 2", "OpenAI"),
        };
        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliProvider = "Ollama", PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.SelectedModel!.Id.Should().Be("m1");
    }

    [Fact]
    public void EvaluateSelection_CliProviderMultipleMatches_InteractiveMode_UsesPromptSelector()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "Ollama"),
            new("m2", "Model 2", "Ollama"),
            new("m3", "Model 3", "OpenAI"),
        };
        var promptCalls = 0;

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliProvider = "ollama", PrintMode = false },
            models,
            defaultProvider: null,
            defaultModel: null,
            promptSelector: candidates =>
            {
                promptCalls++;
                candidates.Should().HaveCount(2);
                return candidates[1];
            });

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("m2");
        promptCalls.Should().Be(1);
    }

    [Fact]
    public void EvaluateSelection_CliProviderPromptCancellation_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "Ollama"),
            new("m2", "Model 2", "Ollama"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliProvider = "ollama", PrintMode = false },
            models,
            defaultProvider: null,
            defaultModel: null,
            promptSelector: _ => throw new OperationCanceledException());

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Be("Model selection cancelled.");
    }

    [Fact]
    public void EvaluateSelection_CliModel_WithProviderFilter_SelectsMatchingCandidate()
    {
        var models = new List<ProviderModelInfo>
        {
            new("model-a", "Shared Model", "ProviderA"),
            new("model-b", "Shared Model", "ProviderB"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliModel = "shared", CliProvider = "providerb", PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("model-b");
    }

    [Fact]
    public void EvaluateSelection_CliModelNotFound_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "ProviderA"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliModel = "missing", PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("No model matching 'missing' found.");
    }

    [Fact]
    public void EvaluateSelection_CliModelAmbiguous_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("gpt-4.1", "GPT 4.1", "OpenAI"),
            new("gpt-4.1-mini", "GPT 4.1 Mini", "OpenAI"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliModel = "gpt-4", PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("ambiguous");
    }

    [Fact]
    public void EvaluateSelection_CliProviderAmbiguousInPrintMode_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "Azure Local"),
            new("m2", "Model 2", "Foundry Local"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { CliProvider = "local", PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null);

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("ambiguous");
    }

    [Fact]
    public void EvaluateSelection_PersistedDefaults_SelectMatchingModel()
    {
        var models = new List<ProviderModelInfo>
        {
            new("gpt-4.1", "GPT 4.1", "OpenAI"),
            new("qwen", "Qwen", "Ollama"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            models,
            defaultProvider: "openai",
            defaultModel: "gpt-4.1");

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("gpt-4.1");
    }

    [Fact]
    public void EvaluateSelection_PersistedDefaults_RequireExactMatch()
    {
        var models = new List<ProviderModelInfo>
        {
            new("gpt-4.1-mini", "GPT 4.1 Mini", "OpenAI"),
            new("gpt-4.1", "GPT 4.1", "OpenAI"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            models,
            defaultProvider: "openai",
            defaultModel: "gpt-4.1");

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("gpt-4.1");
    }

    [Fact]
    public void EvaluateSelection_PersistedDefaults_ProviderOnlyState_NarrowsCandidates()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "ProviderA"),
            new("m2", "Model 2", "ProviderA"),
            new("m3", "Model 3", "ProviderB"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            models,
            defaultProvider: "ProviderA",
            defaultModel: null,
            router: new StubModelRouter(ModelRouteDecision.None),
            promptSelector: candidates => candidates[1]);

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("m2");
    }

    [Fact]
    public void EvaluateSelection_PersistedDefaults_ModelOnlyUniqueMatch_SelectsModel()
    {
        var models = new List<ProviderModelInfo>
        {
            new("shared", "Shared", "ProviderA"),
            new("unique-model", "Unique Model", "ProviderB"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: "unique-model");

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("unique-model");
    }

    [Fact]
    public void EvaluateSelection_PersistedDefaults_ModelOnlyAmbiguousInPrintMode_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("shared", "Shared", "ProviderA"),
            new("shared", "Shared", "ProviderB"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: "shared");

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Contain("ambiguous");
    }

    [Fact]
    public void EvaluateSelection_DefaultModel_NoMatch_FallsThrough()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "Provider"),
        };
        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: "nonexistent");

        // Should fall through to routing or non-interactive policy
        decision.Handled.Should().BeTrue();
        decision.SelectedModel.Should().NotBeNull();
    }

    [Fact]
    public void EvaluateSelection_RoutingSelectsModel_AndDistinctFallbackIds()
    {
        var selected = new ProviderModelInfo("m2", "Model 2", "ProviderB");
        var duplicateFallback = new ProviderModelInfo("m1", "Model 1", "ProviderA");
        var router = new StubModelRouter(new ModelRouteDecision(
            selected,
            [duplicateFallback, duplicateFallback],
            [],
            "stub"));
        var models = new List<ProviderModelInfo>
        {
            duplicateFallback,
            selected,
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            models,
            defaultProvider: null,
            defaultModel: null,
            router: router);

        decision.Handled.Should().BeTrue();
        decision.SelectedModel.Should().BeSameAs(selected);
        decision.FallbackModelIds.Should().Equal("m1");
    }

    [Fact]
    public void EvaluateSelection_PrintModeWithMultipleModels_SelectsFirstModelWithoutPrompt()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "ProviderA"),
            new("m2", "Model 2", "ProviderB"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            models,
            defaultProvider: null,
            defaultModel: null,
            router: new StubModelRouter(ModelRouteDecision.None),
            promptSelector: _ => throw new InvalidOperationException("Prompt should not be used in print mode"));

        decision.Handled.Should().BeTrue();
        decision.SelectedModel!.Id.Should().Be("m1");
    }

    [Fact]
    public void EvaluateSelection_InteractivePromptCancellation_ReturnsError()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "ProviderA"),
            new("m2", "Model 2", "ProviderB"),
        };

        var decision = ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            models,
            defaultProvider: null,
            defaultModel: null,
            router: new StubModelRouter(ModelRouteDecision.None),
            promptSelector: _ => throw new OperationCanceledException());

        decision.Handled.Should().BeTrue();
        decision.ErrorMessage.Should().Be("Model selection cancelled.");
    }

    [Fact]
    public void EvaluateSelection_InteractivePromptInvalidOperation_Propagates()
    {
        var models = new List<ProviderModelInfo>
        {
            new("m1", "Model 1", "ProviderA"),
            new("m2", "Model 2", "ProviderB"),
        };

        var act = () => ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = false },
            models,
            defaultProvider: null,
            defaultModel: null,
            router: new StubModelRouter(ModelRouteDecision.None),
            promptSelector: _ => throw new InvalidOperationException());

        act.Should().Throw<InvalidOperationException>();
    }

    // ── ModelSelectionDecision factory methods ────────────────────────────

    [Fact]
    public void ModelSelectionDecision_Continue_IsNotHandled()
    {
        var d = ProviderOrchestrator.ModelSelectionDecision.Continue;
        d.Handled.Should().BeFalse();
        d.SelectedModel.Should().BeNull();
        d.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ModelSelectionDecision_Select_IsHandled()
    {
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var d = ProviderOrchestrator.ModelSelectionDecision.Select(model);
        d.Handled.Should().BeTrue();
        d.SelectedModel.Should().Be(model);
        d.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ModelSelectionDecision_Select_WithFallbacks()
    {
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var d = ProviderOrchestrator.ModelSelectionDecision.Select(model, ["fb1", "fb2"]);
        d.FallbackModelIds.Should().HaveCount(2);
    }

    [Fact]
    public void ModelSelectionDecision_Error_IsHandled()
    {
        var d = ProviderOrchestrator.ModelSelectionDecision.Error("something broke");
        d.Handled.Should().BeTrue();
        d.SelectedModel.Should().BeNull();
        d.ErrorMessage.Should().Be("something broke");
    }

    private sealed class StubModelRouter(ModelRouteDecision decision) : IModelRouter
    {
        public ModelRouteDecision Route(IReadOnlyList<ProviderModelInfo> candidates, RoutingPolicy policy) => decision;
    }
}
