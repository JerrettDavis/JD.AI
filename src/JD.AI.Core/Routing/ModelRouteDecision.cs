using JD.AI.Core.Providers;

namespace JD.AI.Core.Routing;

/// <summary>
/// Router output: selected model, fallback chain, and per-candidate scores.
/// </summary>
public sealed record ModelRouteDecision(
    ProviderModelInfo? SelectedModel,
    IReadOnlyList<ProviderModelInfo> FallbackModels,
    IReadOnlyList<ProviderScore> Scores,
    string Strategy)
{
    public static ModelRouteDecision None { get; } = new(
        SelectedModel: null,
        FallbackModels: [],
        Scores: [],
        Strategy: "none");
}