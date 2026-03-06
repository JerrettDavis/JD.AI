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
    public void EvaluateSelection_NoModels_PrintMode_Throws()
    {
        // EvaluateNonInteractivePolicy indexes Models[0] in PrintMode
        // before EvaluateInteractivePolicy can catch empty-list.
        var act = () => ProviderOrchestrator.EvaluateSelection(
            new CliOptions { PrintMode = true },
            [],
            defaultProvider: null,
            defaultModel: null);

        act.Should().Throw<ArgumentOutOfRangeException>();
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
}
