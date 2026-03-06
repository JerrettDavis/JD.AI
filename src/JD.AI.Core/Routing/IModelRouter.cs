using JD.AI.Core.Providers;

namespace JD.AI.Core.Routing;

/// <summary>
/// Selects the best model and ordered fallback chain from available models.
/// </summary>
public interface IModelRouter
{
    ModelRouteDecision Route(
        IReadOnlyList<ProviderModelInfo> candidates,
        RoutingPolicy policy);
}
