using JD.AI.Core.Providers;

namespace JD.AI.Core.Routing;

/// <summary>
/// Policy used by <see cref="IModelRouter"/> to score and select models.
/// </summary>
public sealed record RoutingPolicy(
    RoutingStrategy Strategy,
    ModelCapabilities RequiredCapabilities,
    IReadOnlyList<string> PreferredProviders,
    IReadOnlyList<string> FallbackProviders)
{
    public static RoutingPolicy Default { get; } = new(
        Strategy: RoutingStrategy.LocalFirst,
        RequiredCapabilities: ModelCapabilities.Chat | ModelCapabilities.ToolCalling,
        PreferredProviders: ["Ollama", "Foundry Local", "Local"],
        FallbackProviders: []);
}
