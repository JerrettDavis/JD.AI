using JD.AI.Core.Providers;

namespace JD.AI.Core.Usage;

/// <summary>
/// Default turn-cost estimator.
/// Prefers model metadata, falls back to <see cref="CostRateProvider"/>, then zero.
/// </summary>
public sealed class DefaultCostEstimator : ICostEstimator
{
    private readonly CostRateProvider _rateProvider;

    public DefaultCostEstimator(CostRateProvider? rateProvider = null)
    {
        _rateProvider = rateProvider ?? new CostRateProvider();
    }

    public (decimal InputPerToken, decimal OutputPerToken, string Source) ResolveRates(ProviderModelInfo model)
    {
        if (IsLocalProvider(model.ProviderName))
            return (0m, 0m, "local-free");

        if (model.HasMetadata || model.InputCostPerToken > 0m || model.OutputCostPerToken > 0m)
            return (model.InputCostPerToken, model.OutputCostPerToken, model.HasMetadata ? "model-metadata" : "model-definition");

        var (input, output) = _rateProvider.GetRate(model.ProviderName, model.Id);
        return input > 0m || output > 0m
            ? (input, output, "cost-rate-provider")
            : (0m, 0m, "fallback-zero");
    }

    public decimal EstimateTurnCostUsd(ProviderModelInfo model, long promptTokens, long completionTokens)
    {
        var prompt = Math.Max(0, promptTokens);
        var completion = Math.Max(0, completionTokens);

        var (inputRate, outputRate, _) = ResolveRates(model);
        return (prompt * inputRate) + (completion * outputRate);
    }

    private static bool IsLocalProvider(string providerName) =>
        string.Equals(providerName, "Ollama", StringComparison.OrdinalIgnoreCase)
        || string.Equals(providerName, "Foundry Local", StringComparison.OrdinalIgnoreCase)
        || string.Equals(providerName, "Local", StringComparison.OrdinalIgnoreCase)
        || string.Equals(providerName, "LlamaSharp", StringComparison.OrdinalIgnoreCase);
}
