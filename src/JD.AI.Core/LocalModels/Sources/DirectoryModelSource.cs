namespace JD.AI.Core.LocalModels.Sources;

/// <summary>
/// Recursively scans a directory for GGUF model files.
/// </summary>
public sealed class DirectoryModelSource : IModelSource
{
    private readonly string _directory;

    public DirectoryModelSource(string directory) =>
        _directory = Path.GetFullPath(directory);

    public Task<IReadOnlyList<ModelMetadata>> ScanAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_directory))
        {
            return Task.FromResult<IReadOnlyList<ModelMetadata>>([]);
        }

        var files = Directory.EnumerateFiles(_directory, "*.gguf", SearchOption.AllDirectories);
        var models = new List<ModelMetadata>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            var (quant, paramSize) = ModelMetadata.ParseFilename(info.Name);

            models.Add(new ModelMetadata
            {
                Id = Path.GetFileNameWithoutExtension(info.Name).ToLowerInvariant(),
                DisplayName = ModelMetadata.DisplayNameFromFilename(info.Name),
                FilePath = info.FullName,
                FileSizeBytes = info.Length,
                Quantization = quant,
                ParameterSize = paramSize,
                Source = ModelSourceKind.DirectoryScan,
            });
        }

        return Task.FromResult<IReadOnlyList<ModelMetadata>>(models);
    }
}
