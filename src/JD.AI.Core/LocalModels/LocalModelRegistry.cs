using System.Text.Json;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.LocalModels.Sources;
using Microsoft.Extensions.Logging;

namespace JD.AI.Core.LocalModels;

/// <summary>
/// Manages the local model registry (JSON manifest) and coordinates model sources.
/// </summary>
public sealed class LocalModelRegistry
{
    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Options;

    private readonly string _registryPath;
    private readonly string _modelsDir;
    private readonly ILogger? _logger;
    private ModelRegistry _registry = new();

    public LocalModelRegistry(string? modelsDir = null, ILogger? logger = null)
    {
        _modelsDir = modelsDir ?? GetDefaultModelsDir();
        _registryPath = Path.Combine(_modelsDir, "registry.json");
        _logger = logger;
        Directory.CreateDirectory(_modelsDir);
    }

    public string ModelsDirectory => _modelsDir;
    public IReadOnlyList<ModelMetadata> Models => _registry.Models;

    /// <summary>
    /// Load the registry from disk and scan configured directories.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (File.Exists(_registryPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_registryPath, ct).ConfigureAwait(false);
                _registry = JsonSerializer.Deserialize<ModelRegistry>(json, s_jsonOptions) ?? new();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load model registry from {Path}", _registryPath);
                _registry = new();
            }
        }

        // Remove entries pointing to missing files
        _registry.Models.RemoveAll(m =>
            !string.IsNullOrEmpty(m.FilePath) && !File.Exists(m.FilePath));
    }

    /// <summary>
    /// Scan a directory and merge discovered models into the registry.
    /// </summary>
    public async Task ScanDirectoryAsync(string? directory = null, CancellationToken ct = default)
    {
        var dir = directory ?? _modelsDir;
        var source = new DirectoryModelSource(dir);
        var found = await source.ScanAsync(ct).ConfigureAwait(false);
        MergeModels(found);
    }

    /// <summary>
    /// Add a single model file to the registry.
    /// </summary>
    public async Task AddFileAsync(string filePath, CancellationToken ct = default)
    {
        var source = new FileModelSource(filePath);
        var found = await source.ScanAsync(ct).ConfigureAwait(false);
        MergeModels(found);
    }

    /// <summary>
    /// Add a model metadata record directly.
    /// </summary>
    public void Add(ModelMetadata model)
    {
        MergeModels([model]);
    }

    /// <summary>
    /// Remove a model by ID.
    /// </summary>
    public bool Remove(string id, bool deleteFile = false)
    {
        var existing = _registry.Models.Find(m =>
            string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return false;

        _registry.Models.Remove(existing);

        if (deleteFile && File.Exists(existing.FilePath))
        {
            try { File.Delete(existing.FilePath); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Failed to delete model file {Path}", existing.FilePath); }
        }

        return true;
    }

    /// <summary>
    /// Find a model by ID (exact or substring match).
    /// </summary>
    public ModelMetadata? Find(string query) =>
        _registry.Models.Find(m =>
            string.Equals(m.Id, query, StringComparison.OrdinalIgnoreCase))
        ?? _registry.Models.Find(m =>
            m.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
        ?? _registry.Models.Find(m =>
            m.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Persist the registry to disk.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_registryPath)!);
        var json = JsonSerializer.Serialize(_registry, s_jsonOptions);
        await File.WriteAllTextAsync(_registryPath, json, ct).ConfigureAwait(false);
    }

    private void MergeModels(IReadOnlyList<ModelMetadata> incoming)
    {
        foreach (var model in incoming)
        {
            // Deduplicate by file path (primary) or ID
            var existing = _registry.Models.FindIndex(m =>
                string.Equals(m.FilePath, model.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                _registry.Models[existing] = model;
            }
            else
            {
                var byId = _registry.Models.FindIndex(m =>
                    string.Equals(m.Id, model.Id, StringComparison.OrdinalIgnoreCase));

                if (byId >= 0)
                {
                    // Same ID but different path — suffix the new one
                    var uniqueId = $"{model.Id}-{model.FilePath.GetHashCode(StringComparison.Ordinal):x8}";
                    _registry.Models.Add(model with { Id = uniqueId });
                }
                else
                {
                    _registry.Models.Add(model);
                }
            }
        }
    }

    private static string GetDefaultModelsDir()
    {
        var custom = Environment.GetEnvironmentVariable("JDAI_MODELS_DIR");
        if (!string.IsNullOrEmpty(custom)) return custom;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai",
            "models");
    }
}
