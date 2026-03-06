using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Routing;

namespace JD.AI.Tests.Routing;

public sealed class RoutingModelsTests
{
    // ── RoutingPolicy.Default ───────────────────────────────────────────────

    [Fact]
    public void RoutingPolicy_Default_UsesLocalFirst()
    {
        RoutingPolicy.Default.Strategy.Should().Be(RoutingStrategy.LocalFirst);
    }

    [Fact]
    public void RoutingPolicy_Default_RequiresChatAndToolCalling()
    {
        RoutingPolicy.Default.RequiredCapabilities
            .Should().HaveFlag(ModelCapabilities.Chat)
            .And.HaveFlag(ModelCapabilities.ToolCalling);
    }

    [Fact]
    public void RoutingPolicy_Default_HasPreferredProviders()
    {
        RoutingPolicy.Default.PreferredProviders.Should().NotBeEmpty();
        RoutingPolicy.Default.PreferredProviders.Should().Contain("Ollama");
    }

    [Fact]
    public void RoutingPolicy_Default_EmptyFallbackProviders()
    {
        RoutingPolicy.Default.FallbackProviders.Should().BeEmpty();
    }

    [Fact]
    public void RoutingPolicy_RecordEquality()
    {
        var a = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.Chat,
            ["Ollama"],
            []);
        var b = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.Chat,
            ["Ollama"],
            []);
        // Record equality uses reference equality for lists
        a.Strategy.Should().Be(b.Strategy);
        a.RequiredCapabilities.Should().Be(b.RequiredCapabilities);
    }

    // ── ModelRouteDecision.None ─────────────────────────────────────────────

    [Fact]
    public void ModelRouteDecision_None_HasNullModel()
    {
        ModelRouteDecision.None.SelectedModel.Should().BeNull();
    }

    [Fact]
    public void ModelRouteDecision_None_EmptyFallbacks()
    {
        ModelRouteDecision.None.FallbackModels.Should().BeEmpty();
    }

    [Fact]
    public void ModelRouteDecision_None_EmptyScores()
    {
        ModelRouteDecision.None.Scores.Should().BeEmpty();
    }

    [Fact]
    public void ModelRouteDecision_None_StrategyIsNone()
    {
        ModelRouteDecision.None.Strategy.Should().Be("none");
    }

    [Fact]
    public void ModelRouteDecision_CustomConstruction()
    {
        var model = new ProviderModelInfo("claude-3", "Claude 3", "Anthropic");
        var fallback = new ProviderModelInfo("gpt-4", "GPT-4", "OpenAI");
        var score = new ProviderScore(model, 0.95, "Local preferred");

        var decision = new ModelRouteDecision(
            model,
            [fallback],
            [score],
            "local-first");

        decision.SelectedModel.Should().Be(model);
        decision.FallbackModels.Should().HaveCount(1);
        decision.Scores.Should().HaveCount(1);
        decision.Strategy.Should().Be("local-first");
    }

    // ── ProviderScore ───────────────────────────────────────────────────────

    [Fact]
    public void ProviderScore_Construction()
    {
        var model = new ProviderModelInfo("test", "Test", "TestProvider");
        var score = new ProviderScore(model, 0.85, "High capability match");

        score.Model.Should().Be(model);
        score.Score.Should().Be(0.85);
        score.Reason.Should().Be("High capability match");
    }

    [Fact]
    public void ProviderScore_RecordEquality()
    {
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var a = new ProviderScore(model, 0.5, "reason");
        var b = new ProviderScore(model, 0.5, "reason");
        a.Should().Be(b);
    }

    [Fact]
    public void ProviderScore_RecordInequality()
    {
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var a = new ProviderScore(model, 0.5, "reason");
        var b = new ProviderScore(model, 0.9, "different");
        a.Should().NotBe(b);
    }
}
