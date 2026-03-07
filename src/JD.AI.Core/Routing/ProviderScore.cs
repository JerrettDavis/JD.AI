using JD.AI.Core.Providers;

namespace JD.AI.Core.Routing;

/// <summary>
/// Score breakdown for one provider/model candidate.
/// </summary>
public sealed record ProviderScore(
    ProviderModelInfo Model,
    double Score,
    string Reason);
