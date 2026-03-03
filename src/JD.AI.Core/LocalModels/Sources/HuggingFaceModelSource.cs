using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.LocalModels.Sources;

/// <summary>
/// Searches HuggingFace Hub for GGUF model files.
/// </summary>
public sealed class HuggingFaceModelSource : IModelSource
{
    private static readonly HttpClient SharedClient = new()
    {
        BaseAddress = new Uri("https://huggingface.co/"),
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly string _cacheDir;

    public HuggingFaceModelSource(string cacheDir) =>
        _cacheDir = cacheDir;

    /// <summary>
    /// Scans locally cached HuggingFace models (does not download).
    /// </summary>
    public Task<IReadOnlyList<ModelMetadata>> ScanAsync(CancellationToken ct = default)
    {
        // Scan the HF cache directory for already-downloaded GGUF files
        var hfHome = Environment.GetEnvironmentVariable("HF_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "huggingface");

        var hubDir = Path.Combine(hfHome, "hub");
        if (!Directory.Exists(hubDir))
        {
            return Task.FromResult<IReadOnlyList<ModelMetadata>>([]);
        }

        var models = new List<ModelMetadata>();
        foreach (var gguf in Directory.EnumerateFiles(hubDir, "*.gguf", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(gguf);
            var (quant, paramSize) = ModelMetadata.ParseFilename(info.Name);

            models.Add(new ModelMetadata
            {
                Id = $"hf:{Path.GetFileNameWithoutExtension(info.Name).ToLowerInvariant()}",
                DisplayName = $"[HF] {ModelMetadata.DisplayNameFromFilename(info.Name)}",
                FilePath = info.FullName,
                FileSizeBytes = info.Length,
                Quantization = quant,
                ParameterSize = paramSize,
                Source = ModelSourceKind.HuggingFace,
            });
        }

        return Task.FromResult<IReadOnlyList<ModelMetadata>>(models);
    }

    /// <summary>
    /// Search HuggingFace Hub API for GGUF model repos.
    /// </summary>
    public async Task<IReadOnlyList<HuggingFaceSearchResult>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken ct = default)
    {
        var url = $"api/models?search={Uri.EscapeDataString(query)}&filter=gguf&sort=downloads&direction=-1&limit={limit}";

        var token = Environment.GetEnvironmentVariable("HF_TOKEN");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token))
        {
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var resp = await SharedClient.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<HuggingFaceSearchResult>>(ct).ConfigureAwait(false) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>
    /// Construct a download URL for a specific file in a HuggingFace repo.
    /// </summary>
    public static Uri GetDownloadUrl(string repoId, string filename) =>
        new($"https://huggingface.co/{repoId}/resolve/main/{filename}");
}

/// <summary>
/// A model repository from the HuggingFace Hub API.
/// </summary>
public sealed record HuggingFaceSearchResult
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("downloads")]
    public long Downloads { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }
}
