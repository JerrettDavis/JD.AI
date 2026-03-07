using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Routing;

namespace JD.AI.Tests.Routing;

public sealed class DefaultModelRouterExtendedTests
{
    private readonly DefaultModelRouter _router = new();

    // ── Empty / None ────────────────────────────────────────────────────

    [Fact]
    public void Route_EmptyCandidates_ReturnsNone()
    {
        var decision = _router.Route([], RoutingPolicy.Default);
        decision.Should().Be(ModelRouteDecision.None);
    }

    // ── LatencyOptimized strategy ───────────────────────────────────────

    [Fact]
    public void Route_LatencyOptimized_PrefersLocalProvider()
    {
        var policy = new RoutingPolicy(
            RoutingStrategy.LatencyOptimized,
            ModelCapabilities.Chat,
            [], []);

        var decision = _router.Route(
        [
            new("cloud", "Cloud", "OpenAI", ContextWindowTokens: 128_000),
            new("local", "Local", "Ollama", ContextWindowTokens: 8_000),
        ], policy);

        decision.SelectedModel!.Id.Should().Be("local");
    }

    // ── PreferredProviders boost ────────────────────────────────────────

    [Fact]
    public void Route_PreferredProviders_BoostsMatchingProvider()
    {
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.Chat,
            PreferredProviders: ["Anthropic"],
            FallbackProviders: []);

        var decision = _router.Route(
        [
            new("oai", "GPT", "OpenAI", InputCostPerToken: 0m, OutputCostPerToken: 0m),
            new("claude", "Claude", "Anthropic", InputCostPerToken: 0m, OutputCostPerToken: 0m),
        ], policy);

        // Both zero cost, but Anthropic gets preference boost
        decision.SelectedModel!.Id.Should().Be("claude");
    }

    // ── Capability filter fallback ──────────────────────────────────────

    [Fact]
    public void Route_NoCapabilityMatch_FallsBackToAllCandidates()
    {
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.Vision | ModelCapabilities.Embeddings,
            [], []);

        var decision = _router.Route(
        [
            new("chat-only", "Chat", "OpenAI", Capabilities: ModelCapabilities.Chat),
        ], policy);

        // No model has Vision+Embeddings, so falls back to all
        decision.SelectedModel.Should().NotBeNull();
        decision.SelectedModel!.Id.Should().Be("chat-only");
    }

    // ── Scores populated ────────────────────────────────────────────────

    [Fact]
    public void Route_Scores_ContainAllCandidates()
    {
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.None,
            [], []);

        var decision = _router.Route(
        [
            new("a", "Model A", "P1"),
            new("b", "Model B", "P2"),
            new("c", "Model C", "P3"),
        ], policy);

        decision.Scores.Should().HaveCount(3);
        decision.Strategy.Should().Be("CostOptimized");
    }

    // ── FallbackModels ──────────────────────────────────────────────────

    [Fact]
    public void Route_SingleCandidate_EmptyFallback()
    {
        var decision = _router.Route(
        [
            new("only", "Only", "Ollama"),
        ], RoutingPolicy.Default);

        decision.SelectedModel!.Id.Should().Be("only");
        decision.FallbackModels.Should().BeEmpty();
    }

    // ── ModelRouteDecision.None sentinel ─────────────────────────────────

    [Fact]
    public void ModelRouteDecision_None_HasNullSelected()
    {
        ModelRouteDecision.None.SelectedModel.Should().BeNull();
        ModelRouteDecision.None.FallbackModels.Should().BeEmpty();
        ModelRouteDecision.None.Scores.Should().BeEmpty();
        ModelRouteDecision.None.Strategy.Should().Be("none");
    }

    // ── RoutingPolicy.Default ───────────────────────────────────────────

    [Fact]
    public void RoutingPolicy_Default_IsLocalFirst()
    {
        var p = RoutingPolicy.Default;
        p.Strategy.Should().Be(RoutingStrategy.LocalFirst);
        p.RequiredCapabilities.Should().HaveFlag(ModelCapabilities.Chat);
        p.RequiredCapabilities.Should().HaveFlag(ModelCapabilities.ToolCalling);
        p.PreferredProviders.Should().NotBeEmpty();
        p.FallbackProviders.Should().BeEmpty();
    }

    // ── ProviderScore record ────────────────────────────────────────────

    [Fact]
    public void ProviderScore_RecordEquality()
    {
        var model = new ProviderModelInfo("a", "A", "P");
        var a = new ProviderScore(model, 42.5, "reason");
        var b = new ProviderScore(model, 42.5, "reason");
        a.Should().Be(b);
    }
}
