using JD.AI.Core.Providers;
using JD.AI.Core.Routing;

namespace JD.AI.Tests.Routing;

public sealed class DefaultModelRouterTests
{
    [Fact]
    public void Route_LocalFirst_PrefersLocalProvider()
    {
        var router = new DefaultModelRouter();
        var policy = RoutingPolicy.Default;

        var decision = router.Route(
        [
            new("cloud-a", "Cloud A", "OpenAI"),
            new("local-a", "Local A", "Ollama"),
        ],
        policy);

        Assert.Equal("local-a", decision.SelectedModel?.Id);
    }

    [Fact]
    public void Route_CostOptimized_PrefersLowestCostModel()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.CostOptimized,
            ModelCapabilities.Chat,
            [],
            []);

        var decision = router.Route(
        [
            new("expensive", "Expensive", "OpenAI", InputCostPerToken: 0.00002m, OutputCostPerToken: 0.00004m),
            new("cheap", "Cheap", "Ollama", InputCostPerToken: 0m, OutputCostPerToken: 0m),
        ],
        policy);

        Assert.Equal("cheap", decision.SelectedModel?.Id);
    }

    [Fact]
    public void Route_CapabilityDriven_FiltersByRequiredCapabilities()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.CapabilityDriven,
            ModelCapabilities.Chat | ModelCapabilities.Vision,
            [],
            []);

        var decision = router.Route(
        [
            new("chat-only", "Chat", "OpenAI", Capabilities: ModelCapabilities.Chat),
            new("vision", "Vision", "OpenAI", Capabilities: ModelCapabilities.Chat | ModelCapabilities.Vision),
        ],
        policy);

        Assert.Equal("vision", decision.SelectedModel?.Id);
    }

    [Fact]
    public void Route_FallbackProviderOrder_IsRespected()
    {
        var router = new DefaultModelRouter();
        var policy = new RoutingPolicy(
            RoutingStrategy.LocalFirst,
            ModelCapabilities.Chat,
            [],
            ["OpenAI", "OpenRouter"]);

        var decision = router.Route(
        [
            new("local-a", "Local", "Ollama"),
            new("or-a", "OR", "OpenRouter"),
            new("oa-a", "OA", "OpenAI"),
        ],
        policy);

        Assert.Equal("local-a", decision.SelectedModel?.Id);
        Assert.Equal(["oa-a", "or-a"], decision.FallbackModels.Select(m => m.Id).ToArray());
    }
}
