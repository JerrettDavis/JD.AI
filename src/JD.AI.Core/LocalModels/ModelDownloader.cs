namespace JD.AI.Core.LocalModels;

/// <summary>
/// Shared download logic with progress, resume support, retry, and cancellation.
/// </summary>
public sealed class ModelDownloader
{
    private readonly string _cacheDir;
    private const int MaxRetries = 3;

    public ModelDownloader(string? cacheDir = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai", "models");
        Directory.CreateDirectory(_cacheDir);
    }

    public string CacheDirectory => _cacheDir;

    /// <summary>
    /// Downloads a model from a URL with retry logic and progress reporting.
    /// </summary>
    public async Task<ModelMetadata> DownloadAsync(
        Uri url,
        IProgress<(long downloaded, long? total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        var source = new Sources.RemoteUrlModelSource(_cacheDir);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                progress?.Report((0, null, $"Downloading (attempt {attempt}/{MaxRetries})..."));

                var innerProgress = progress is null
                    ? null
                    : new Progress<(long downloaded, long? total)>(p =>
                        progress.Report((p.downloaded, p.total, FormatProgress(p.downloaded, p.total))));

                return await source.DownloadAsync(url, innerProgress, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    progress?.Report((0, null, $"Retry in {delay.TotalSeconds:F0}s..."));
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        throw new InvalidOperationException(
            $"Download failed after {MaxRetries} attempts: {lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// Downloads a model from HuggingFace by repo ID and optional filename.
    /// </summary>
    public async Task<ModelMetadata> DownloadFromHuggingFaceAsync(
        string repoId,
        string? filename = null,
        IProgress<(long downloaded, long? total, string status)>? progress = null,
        CancellationToken ct = default)
    {
        filename ??= await FindBestGgufFilename(repoId, ct).ConfigureAwait(false);
        var url = Sources.HuggingFaceModelSource.GetDownloadUrl(repoId, filename);
        return await DownloadAsync(url, progress, ct).ConfigureAwait(false);
    }

    private static async Task<string> FindBestGgufFilename(string repoId, CancellationToken ct)
    {
        // Try to find GGUF files in the HuggingFace repo via API
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var token = Environment.GetEnvironmentVariable("HF_TOKEN");

        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://huggingface.co/api/models/{repoId}");
        if (!string.IsNullOrEmpty(token))
        {
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        // Simple regex to find .gguf filenames — avoids a full JSON parse for siblings
        var matches = System.Text.RegularExpressions.Regex.Matches(json, @"""([^""]+\.gguf)""");
        var ggufFiles = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

        if (ggufFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No GGUF files found in HuggingFace repo '{repoId}'");
        }

        // Prefer Q4_K_M or Q5_K_M if available (good quality/size balance)
        return ggufFiles.Find(f => f.Contains("Q4_K_M", StringComparison.OrdinalIgnoreCase))
            ?? ggufFiles.Find(f => f.Contains("Q5_K_M", StringComparison.OrdinalIgnoreCase))
            ?? ggufFiles.Find(f => f.Contains("Q4", StringComparison.OrdinalIgnoreCase))
            ?? ggufFiles[0];
    }

    private static string FormatProgress(long downloaded, long? total)
    {
        var dl = FormatBytes(downloaded);
        return total.HasValue
            ? $"{dl} / {FormatBytes(total.Value)} ({100.0 * downloaded / total.Value:F1}%)"
            : $"{dl} downloaded";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB",
        _ => $"{bytes} B",
    };
}
