using JD.AI.Core.Providers.Metadata;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Aggregates all <see cref="IProviderDetector"/> instances and exposes
/// a unified model catalog with kernel-building capability.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly IReadOnlyList<IProviderDetector> _detectors;
    private readonly ModelMetadataProvider? _metadataProvider;
    private List<ProviderInfo>? _cached;

    public ProviderRegistry(
        IEnumerable<IProviderDetector> detectors,
        ModelMetadataProvider? metadataProvider = null)
    {
        _detectors = detectors.ToList();
        _metadataProvider = metadataProvider;
    }

    public async Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(
        CancellationToken ct = default)
    {
        // Kick off metadata loading concurrently with detector probes
        var metadataTask = _metadataProvider?.LoadAsync(ct: ct);

        var results = new List<ProviderInfo>();
        foreach (var detector in _detectors)
        {
            try
            {
                results.Add(await detector.DetectAsync(ct).ConfigureAwait(false));
            }
#pragma warning disable CA1031 // catch broad — detector failures are non-fatal
            catch (Exception ex)
#pragma warning restore CA1031
            {
                results.Add(new ProviderInfo(
                    detector.ProviderName,
                    IsAvailable: false,
                    StatusMessage: ex.Message,
                    Models: []));
            }
        }

        // Await metadata and enrich models
        if (metadataTask is not null)
        {
            await metadataTask.ConfigureAwait(false);
            for (var i = 0; i < results.Count; i++)
            {
                var provider = results[i];
                if (provider.IsAvailable && provider.Models.Count > 0)
                {
                    var enriched = _metadataProvider!.Enrich(provider.Models);
                    results[i] = provider with { Models = enriched };
                }
            }
        }

        _cached = results;
        return results;
    }

    public async Task<IReadOnlyList<ProviderModelInfo>> GetModelsAsync(
        CancellationToken ct = default)
    {
        var providers = _cached ?? await DetectProvidersAsync(ct).ConfigureAwait(false);
        return providers
            .Where(p => p.IsAvailable)
            .SelectMany(p => p.Models)
            .ToList();
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var detector = _detectors.FirstOrDefault(
            d => string.Equals(d.ProviderName, model.ProviderName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No detector registered for provider '{model.ProviderName}'.");

        return detector.BuildKernel(model);
    }

    public IProviderDetector? GetDetector(string providerName)
    {
        return _detectors.FirstOrDefault(
            d => string.Equals(d.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
    }
}
