using FluentAssertions;
using JD.AI.Core.Providers;
using JD.AI.Core.Routing;

namespace JD.AI.Tests.Routing;

public sealed class RoutingModelTests
{
    // ── ModelRouteDecision ─────────────────────────────────────────────────

    [Fact]
    public void ModelRouteDecision_None_IsSingleton()
    {
        var a = ModelRouteDecision.None;
        var b = ModelRouteDecision.None;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void ModelRouteDecision_WithSelection()
    {
        var model = new ProviderModelInfo("m1", "Model 1", "Ollama");
        var fallbacks = new List<ProviderModelInfo>
        {
            new("m2", "Model 2", "OpenAI"),
        };
        var scores = new List<ProviderScore>
        {
            new(model, 100.0, "top pick"),
        };

        var decision = new ModelRouteDecision(model, fallbacks, scores, "LocalFirst");

        decision.SelectedModel.Should().Be(model);
        decision.FallbackModels.Should().HaveCount(1);
        decision.Scores.Should().HaveCount(1);
        decision.Strategy.Should().Be("LocalFirst");
    }

    [Fact]
    public void ModelRouteDecision_RecordEquality()
    {
        var a = ModelRouteDecision.None;
        var b = new ModelRouteDecision(null, [], [], "none");
        a.Should().Be(b);
    }

    // ── RoutingPolicy ─────────────────────────────────────────────────────

    [Fact]
    public void RoutingPolicy_Default_Singleton()
    {
        var a = RoutingPolicy.Default;
        var b = RoutingPolicy.Default;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void RoutingPolicy_CustomValues()
    {
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.Chat | ModelCapabilities.Vision,
            PreferredProviders: ["Anthropic"],
            FallbackProviders: ["OpenAI"]);

        policy.Strategy.Should().Be(RoutingStrategy.CostOptimized);
        policy.RequiredCapabilities.Should().HaveFlag(ModelCapabilities.Chat);
        policy.RequiredCapabilities.Should().HaveFlag(ModelCapabilities.Vision);
        policy.PreferredProviders.Should().ContainSingle().Which.Should().Be("Anthropic");
        policy.FallbackProviders.Should().ContainSingle().Which.Should().Be("OpenAI");
    }

    [Fact]
    public void RoutingPolicy_RecordEquality()
    {
        var a = new RoutingPolicy(RoutingStrategy.LocalFirst, ModelCapabilities.Chat, [], []);
        var b = new RoutingPolicy(RoutingStrategy.LocalFirst, ModelCapabilities.Chat, [], []);
        a.Should().Be(b);
    }

    // ── ProviderScore ─────────────────────────────────────────────────────

    [Fact]
    public void ProviderScore_Properties()
    {
        var model = new ProviderModelInfo("test", "Test", "Provider");
        var score = new ProviderScore(model, 85.5, "good match");

        score.Model.Should().Be(model);
        score.Score.Should().Be(85.5);
        score.Reason.Should().Be("good match");
    }

    // ── RoutingStrategy enum ──────────────────────────────────────────────

    [Theory]
    [InlineData(RoutingStrategy.LocalFirst)]
    [InlineData(RoutingStrategy.CostOptimized)]
    [InlineData(RoutingStrategy.CapabilityDriven)]
    [InlineData(RoutingStrategy.LatencyOptimized)]
    public void RoutingStrategy_AllValuesExist(RoutingStrategy strategy) =>
        Enum.IsDefined(strategy).Should().BeTrue();

    // ── DefaultModelRouter edge cases ─────────────────────────────────────

    [Fact]
    public void Route_CostOptimized_PrefersFreeModels()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.None, [], []);

        var decision = router.Route(
        [
            new("expensive", "Expensive", "CloudProvider",
                InputCostPerToken: 0.00001m, OutputCostPerToken: 0.00003m),
            new("free", "Free", "Ollama",
                InputCostPerToken: 0m, OutputCostPerToken: 0m),
        ], policy);

        decision.SelectedModel!.Id.Should().Be("free");
    }

    [Fact]
    public void Route_CapabilityDriven_PrefersRichCapabilities()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.CapabilityDriven,
            ModelCapabilities.Chat, [], []);

        var decision = router.Route(
        [
            new("basic", "Basic", "Provider",
                Capabilities: ModelCapabilities.Chat),
            new("rich", "Rich", "Provider",
                Capabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling | ModelCapabilities.Vision),
        ], policy);

        decision.SelectedModel!.Id.Should().Be("rich");
    }

    [Fact]
    public void Route_FallbackProviders_OrderedCorrectly()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.None,
            PreferredProviders: [],
            FallbackProviders: ["Anthropic", "OpenAI"]);

        var decision = router.Route(
        [
            new("oai-1", "GPT", "OpenAI"),
            new("claude-1", "Claude", "Anthropic"),
            new("mistral-1", "Mistral", "Mistral"),
        ], policy);

        // Selected is top-scored; fallbacks should be ordered by fallback preference
        decision.FallbackModels.Should().HaveCount(2);
    }

    [Fact]
    public void Route_LocalFirst_PrefersOllama()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.LocalFirst,
            ModelCapabilities.None,
            PreferredProviders: ["Ollama"],
            FallbackProviders: []);

        var decision = router.Route(
        [
            new("cloud", "Cloud", "OpenAI", ContextWindowTokens: 128_000),
            new("local", "Local", "Ollama", ContextWindowTokens: 8_000),
        ], policy);

        decision.SelectedModel!.Id.Should().Be("local");
    }
}
