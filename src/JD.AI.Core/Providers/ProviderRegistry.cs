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
    private readonly IModelCapabilityRegistry _capabilityRegistry;
    private List<ProviderInfo>? _cached;

    public ProviderRegistry(
        IEnumerable<IProviderDetector> detectors,
        ModelMetadataProvider? metadataProvider = null,
        IModelCapabilityRegistry? capabilityRegistry = null)
    {
        _detectors = detectors.ToList();
        _metadataProvider = metadataProvider;
        _capabilityRegistry = capabilityRegistry ?? new ModelCapabilityRegistry();
    }

    /// <summary>
    /// Capability-aware model metadata registry populated on provider detection.
    /// </summary>
    public IModelCapabilityRegistry CapabilityRegistry => _capabilityRegistry;

    public Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(
        CancellationToken ct = default)
        => DetectProvidersAsync(forceRefresh: false, ct);

    public async Task<IReadOnlyList<ProviderInfo>> DetectProvidersAsync(
        bool forceRefresh,
        CancellationToken ct = default)
    {
        if (!forceRefresh && _cached is not null)
            return _cached;

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

        RebuildCapabilityRegistry(results);
        _cached = results;
        return results;
    }

    public Task<IReadOnlyList<ProviderModelInfo>> GetModelsAsync(
        CancellationToken ct = default)
        => GetModelsAsync(forceRefresh: false, ct);

    public async Task<IReadOnlyList<ProviderModelInfo>> GetModelsAsync(
        bool forceRefresh,
        CancellationToken ct = default)
    {
        var providers = forceRefresh
            ? await DetectProvidersAsync(forceRefresh: true, ct).ConfigureAwait(false)
            : _cached ?? await DetectProvidersAsync(ct).ConfigureAwait(false);
        return providers
            .Where(p => p.IsAvailable)
            .SelectMany(p => p.Models)
            .ToList();
    }

    public async Task<ProviderInfo?> DetectProviderAsync(
        string providerName,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return null;

        if (!forceRefresh && _cached is not null)
        {
            var cached = _cached.FirstOrDefault(p =>
                string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase));
            if (cached is not null)
                return cached;
        }

        var detector = _detectors.FirstOrDefault(d =>
            string.Equals(d.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (detector is null)
            return null;

        ProviderInfo provider;
        try
        {
            provider = await detector.DetectAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // detector failures are non-fatal
        catch (Exception ex)
#pragma warning restore CA1031
        {
            provider = new ProviderInfo(
                detector.ProviderName,
                IsAvailable: false,
                StatusMessage: ex.Message,
                Models: []);
        }

        if (_metadataProvider is not null && provider.IsAvailable && provider.Models.Count > 0)
        {
            await _metadataProvider.LoadAsync(ct: ct).ConfigureAwait(false);
            provider = provider with { Models = _metadataProvider.Enrich(provider.Models) };
        }

        var updated = _cached?.ToList() ?? [];
        var existingIndex = updated.FindIndex(p =>
            string.Equals(p.Name, provider.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            updated[existingIndex] = provider;
        else
            updated.Add(provider);

        _cached = updated;
        RebuildCapabilityRegistry(updated);
        return provider;
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

    private void RebuildCapabilityRegistry(IEnumerable<ProviderInfo> providers)
    {
        _capabilityRegistry.Clear();
        foreach (var provider in providers.Where(p => p.IsAvailable && p.Models.Count > 0))
            _capabilityRegistry.RegisterRange(provider.Models);
    }
}
