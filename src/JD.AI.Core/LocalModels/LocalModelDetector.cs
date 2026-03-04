using JD.AI.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.LocalModels;

/// <summary>
/// Detects locally available GGUF models and provides LLamaSharp-based inference.
/// </summary>
public sealed class LocalModelDetector : IProviderDetector
{
    private readonly LocalModelRegistry _registry;
    private readonly LocalModelOptions _options;
    private readonly ILogger? _logger;
    private LlamaInferenceEngine? _activeEngine;

    public LocalModelDetector(
        LocalModelRegistry? registry = null,
        LocalModelOptions? options = null,
        ILogger? logger = null)
    {
        _registry = registry ?? new LocalModelRegistry(logger: logger);
        _options = options ?? new();
        _logger = logger;
    }

    public string ProviderName => "Local";

    /// <summary>
    /// The registry backing this detector.
    /// </summary>
    public LocalModelRegistry Registry => _registry;

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            await _registry.LoadAsync(ct).ConfigureAwait(false);
            await _registry.ScanDirectoryAsync(ct: ct).ConfigureAwait(false);
            await _registry.SaveAsync(ct).ConfigureAwait(false);

            var models = _registry.Models
                .Select(m => new ProviderModelInfo(
                    m.Id,
                    FormatDisplayName(m),
                    ProviderName,
                    m.Capabilities != ModelCapabilities.None
                        ? m.Capabilities
                        : ModelCapabilityHeuristics.InferFromName(m.Id)))
                .ToList();

            if (models.Count == 0)
            {
                return new ProviderInfo(
                    ProviderName,
                    IsAvailable: false,
                    StatusMessage: $"No models found — add .gguf files to {_registry.ModelsDirectory}",
                    Models: []);
            }

            var gpuBackend = GpuDetector.Detect();
            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"{models.Count} model(s) [{gpuBackend}]",
                Models: models);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Local model detection failed");
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: $"Detection error: {ex.Message}",
                Models: []);
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var metadata = _registry.Find(model.Id)
            ?? throw new InvalidOperationException($"Model '{model.Id}' not found in registry");

        // Dispose previous engine if switching models
        if (_activeEngine is not null)
        {
            _activeEngine.Dispose();
            _activeEngine = null;
        }

        var engine = new LlamaInferenceEngine(metadata, _options, _logger);
        engine.Load();
        _activeEngine = engine;

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>(engine);
        return builder.Build();
    }

    private static string FormatDisplayName(ModelMetadata m)
    {
        var parts = new List<string> { m.DisplayName };

        if (m.ParameterSize is not null)
            parts.Add($"({m.ParameterSize})");

        if (m.Quantization != QuantizationType.Unknown)
            parts.Add($"[{m.Quantization}]");

        var sizeMb = m.FileSizeBytes / (1024.0 * 1024.0);
        parts.Add(sizeMb >= 1024
            ? $"{sizeMb / 1024.0:F1}GB"
            : $"{sizeMb:F0}MB");

        return string.Join(" ", parts);
    }
}
