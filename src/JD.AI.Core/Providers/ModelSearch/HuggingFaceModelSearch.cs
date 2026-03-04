using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Providers.ModelSearch;

/// <summary>
/// Searches the HuggingFace Hub for text-generation models.
/// </summary>
public sealed class HuggingFaceModelSearch : IRemoteModelSearch
{
    private readonly HttpClient _http;
    private static readonly string ModelsDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".jdai", "models");

    public HuggingFaceModelSearch(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public string ProviderName => "HuggingFace";

    internal static string BuildSearchUrl(string query) =>
        string.Create(CultureInfo.InvariantCulture,
            $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&pipeline_tag=text-generation&sort=downloads&direction=-1&limit=20");

    public async Task<IReadOnlyList<RemoteModelResult>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        try
        {
            var url = BuildSearchUrl(query);
            var models = await _http
                .GetFromJsonAsync<List<HfModel>>(url, ct)
                .ConfigureAwait(false);

            return (models ?? [])
                .Select(m =>
                {
                    var installed = IsInstalled(m.ModelId);
                    return new RemoteModelResult(
                        m.ModelId ?? "unknown",
                        m.ModelId ?? "unknown",
                        ProviderName,
                        FormatDownloads(m.Downloads),
                        installed ? "Installed" : "Available",
                        m.Tags is { Count: > 0 }
                            ? string.Join(", ", m.Tags.Take(5))
                            : null);
                })
                .ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public Task<bool> PullAsync(
        RemoteModelResult model,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // Downloading GGUF files from HuggingFace is out of scope;
        // report the model ID so callers can handle it externally.
        progress?.Report($"Model {model.Id} is available on HuggingFace Hub. Use a GGUF downloader to pull it locally.");
        return Task.FromResult(false);
    }

    private static bool IsInstalled(string? modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            return false;
        }

        var safeName = modelId.Replace('/', '_');
        return Directory.Exists(Path.Combine(ModelsDir, safeName));
    }

    private static string? FormatDownloads(long? downloads) =>
        downloads is null or 0
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{downloads:N0} downloads");

    private sealed record HfModel(
        [property: JsonPropertyName("modelId")]
        string? ModelId,
        [property: JsonPropertyName("downloads")]
        long? Downloads,
        [property: JsonPropertyName("tags")]
        List<string>? Tags);
}
