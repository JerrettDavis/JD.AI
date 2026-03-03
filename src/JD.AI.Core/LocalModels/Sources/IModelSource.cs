namespace JD.AI.Core.LocalModels.Sources;

/// <summary>
/// A source that can discover GGUF model files.
/// </summary>
public interface IModelSource
{
    /// <summary>
    /// Scan for available models from this source.
    /// </summary>
    Task<IReadOnlyList<ModelMetadata>> ScanAsync(CancellationToken ct = default);
}
