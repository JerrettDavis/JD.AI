namespace JD.AI.Core.LocalModels.Sources;

/// <summary>
/// Discovers a single GGUF file at a known path.
/// </summary>
public sealed class FileModelSource : IModelSource
{
    private readonly string _filePath;

    public FileModelSource(string filePath) =>
        _filePath = Path.GetFullPath(filePath);

    public Task<IReadOnlyList<ModelMetadata>> ScanAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath) ||
            !_filePath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<ModelMetadata>>([]);
        }

        var info = new FileInfo(_filePath);
        var (quant, paramSize) = ModelMetadata.ParseFilename(info.Name);

        var model = new ModelMetadata
        {
            Id = Path.GetFileNameWithoutExtension(info.Name).ToLowerInvariant(),
            DisplayName = ModelMetadata.DisplayNameFromFilename(info.Name),
            FilePath = _filePath,
            FileSizeBytes = info.Length,
            Quantization = quant,
            ParameterSize = paramSize,
            Source = ModelSourceKind.LocalFile,
        };

        return Task.FromResult<IReadOnlyList<ModelMetadata>>([model]);
    }
}
