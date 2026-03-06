using JD.AI.Core.Providers;
using JD.AI.Core.Usage;

namespace JD.AI.Tests.Usage;

public sealed class DefaultCostEstimatorTests
{
    [Fact]
    public void EstimateTurnCostUsd_UsesModelMetadata_WhenAvailable()
    {
        var estimator = new DefaultCostEstimator();
        var model = new ProviderModelInfo(
            Id: "custom-model",
            DisplayName: "Custom Model",
            ProviderName: "OpenAI",
            InputCostPerToken: 0.000002m,
            OutputCostPerToken: 0.000008m,
            HasMetadata: true);

        var cost = estimator.EstimateTurnCostUsd(model, promptTokens: 1000, completionTokens: 500);
        var (inputRate, outputRate, source) = estimator.ResolveRates(model);

        Assert.Equal(0.006m, cost);
        Assert.Equal(0.000002m, inputRate);
        Assert.Equal(0.000008m, outputRate);
        Assert.Equal("model-metadata", source);
    }

    [Fact]
    public void EstimateTurnCostUsd_FallsBackToRateProvider_WhenMetadataMissing()
    {
        var estimator = new DefaultCostEstimator();
        var model = new ProviderModelInfo(
            Id: "gpt-4.1",
            DisplayName: "gpt-4.1",
            ProviderName: "OpenAI");

        var cost = estimator.EstimateTurnCostUsd(model, promptTokens: 1000, completionTokens: 500);
        var (_, _, source) = estimator.ResolveRates(model);

        Assert.Equal(0.006m, cost);
        Assert.Equal("cost-rate-provider", source);
    }

    [Fact]
    public void EstimateTurnCostUsd_ReturnsZero_ForLocalProvider()
    {
        var estimator = new DefaultCostEstimator();
        var model = new ProviderModelInfo(
            Id: "llama3.2",
            DisplayName: "llama3.2",
            ProviderName: "Ollama",
            InputCostPerToken: 0.0001m,
            OutputCostPerToken: 0.0002m,
            HasMetadata: true);

        var cost = estimator.EstimateTurnCostUsd(model, promptTokens: 1000, completionTokens: 1000);
        var (_, _, source) = estimator.ResolveRates(model);

        Assert.Equal(0m, cost);
        Assert.Equal("local-free", source);
    }

    [Fact]
    public void EstimateTurnCostUsd_ClampsNegativeTokensToZero()
    {
        var estimator = new DefaultCostEstimator();
        var model = new ProviderModelInfo(
            Id: "gpt-4.1",
            DisplayName: "gpt-4.1",
            ProviderName: "OpenAI");

        var cost = estimator.EstimateTurnCostUsd(model, promptTokens: -100, completionTokens: -50);

        Assert.Equal(0m, cost);
    }
}
