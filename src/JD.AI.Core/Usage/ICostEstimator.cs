using JD.AI.Core.Providers;

namespace JD.AI.Core.Usage;

/// <summary>
/// Estimates token spend for a model/provider pair and exposes the resolved rates.
/// </summary>
public interface ICostEstimator
{
    /// <summary>
    /// Resolve effective input/output rates for a model.
    /// Returns (inputPerToken, outputPerToken, source).
    /// </summary>
    (decimal InputPerToken, decimal OutputPerToken, string Source) ResolveRates(ProviderModelInfo model);

    /// <summary>
    /// Estimate USD cost for a turn from prompt/completion token counts.
    /// </summary>
    decimal EstimateTurnCostUsd(ProviderModelInfo model, long promptTokens, long completionTokens);
}
