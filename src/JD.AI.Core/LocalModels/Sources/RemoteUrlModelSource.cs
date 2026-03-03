namespace JD.AI.Core.LocalModels.Sources;

/// <summary>
/// Downloads a GGUF model from a remote HTTP(S) URL.
/// </summary>
public sealed class RemoteUrlModelSource : IModelSource
{
    private readonly string _cacheDir;
    private readonly List<string> _downloadedPaths = [];

    public RemoteUrlModelSource(string cacheDir) =>
        _cacheDir = cacheDir;

    /// <summary>
    /// Returns models that have been downloaded via this source.
    /// </summary>
    public Task<IReadOnlyList<ModelMetadata>> ScanAsync(CancellationToken ct = default) =>
        new DirectoryModelSource(_cacheDir).ScanAsync(ct);

    /// <summary>
    /// Downloads a GGUF file from a URL with progress reporting.
    /// Supports resume via HTTP Range headers.
    /// </summary>
    public async Task<ModelMetadata> DownloadAsync(
        Uri url,
        IProgress<(long downloaded, long? total)>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(_cacheDir);
        var filename = Path.GetFileName(url.LocalPath);
        if (string.IsNullOrEmpty(filename) || !filename.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            filename = $"model-{Guid.NewGuid():N}.gguf";
        }

        var destPath = Path.Combine(_cacheDir, filename);
        long existingBytes = 0;

        if (File.Exists(destPath))
        {
            existingBytes = new FileInfo(destPath).Length;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingBytes > 0)
        {
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
        }

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        // If server doesn't support range, start over
        if (existingBytes > 0 && resp.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            existingBytes = 0;
        }

        resp.EnsureSuccessStatusCode();

        var totalBytes = resp.Content.Headers.ContentLength.HasValue
            ? resp.Content.Headers.ContentLength.Value + existingBytes
            : (long?)null;

        await using var responseStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destPath,
            existingBytes > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920);

        var buffer = new byte[81920];
        long downloaded = existingBytes;
        int bytesRead;

        while ((bytesRead = await responseStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            downloaded += bytesRead;
            progress?.Report((downloaded, totalBytes));
        }

        var info = new FileInfo(destPath);
        var (quant, paramSize) = ModelMetadata.ParseFilename(info.Name);

        var model = new ModelMetadata
        {
            Id = Path.GetFileNameWithoutExtension(info.Name).ToLowerInvariant(),
            DisplayName = ModelMetadata.DisplayNameFromFilename(info.Name),
            FilePath = info.FullName,
            FileSizeBytes = info.Length,
            Quantization = quant,
            ParameterSize = paramSize,
            Source = ModelSourceKind.RemoteUrl,
            SourceUri = url.ToString(),
        };

        _downloadedPaths.Add(destPath);
        return model;
    }
}
