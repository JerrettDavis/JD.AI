using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects and registers AI providers, builds kernels on demand.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>
    /// Probes all known provider backends and returns availability info.
    /// </summary>
    Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Returns all models across all available providers.
    /// </summary>
    Task<IReadOnlyList<ProviderModelInfo>> GetModelsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Builds a <see cref="Kernel"/> configured for the given model.
    /// </summary>
    Kernel BuildKernel(ProviderModelInfo model);

    /// <summary>
    /// Returns the <see cref="IProviderDetector"/> for the given provider name.
    /// </summary>
    IProviderDetector? GetDetector(string providerName);
}
