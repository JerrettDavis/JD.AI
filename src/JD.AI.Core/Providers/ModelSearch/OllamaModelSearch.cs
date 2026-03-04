using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Providers.ModelSearch;

/// <summary>
/// Searches for models available through a local Ollama instance.
/// </summary>
public sealed class OllamaModelSearch : IRemoteModelSearch
{
    private readonly HttpClient _http;
    private readonly string _endpoint;

    public OllamaModelSearch(HttpClient httpClient)
    {
        _http = httpClient;
        _endpoint = (Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
                     ?? "http://localhost:11434").TrimEnd('/');
    }

    public string ProviderName => "Ollama";

    public async Task<IReadOnlyList<RemoteModelResult>> SearchAsync(
        string query, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http
                .GetFromJsonAsync<OllamaTagsResponse>(
                    $"{_endpoint}/api/tags", ct)
                .ConfigureAwait(false);

            var models = (resp?.Models ?? [])
                .Where(m => string.IsNullOrWhiteSpace(query)
                            || (m.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(m => new RemoteModelResult(
                    m.Name ?? "unknown",
                    m.Name ?? "unknown",
                    ProviderName,
                    FormatBytes(m.Size),
                    "Installed",
                    null))
                .ToList();

            return models;
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<bool> PullAsync(
        RemoteModelResult model,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = $"pull {model.Id}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            await Task.WhenAll(
                ReadStreamAsync(process.StandardOutput, progress, ct),
                ReadStreamAsync(process.StandardError, progress, ct))
                .ConfigureAwait(false);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report($"Error: {ex.Message}");
            return false;
        }
    }

    private static async Task ReadStreamAsync(
        System.IO.StreamReader reader,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            progress?.Report(line);
        }
    }

    private static string? FormatBytes(long? bytes)
    {
        if (bytes is null or 0)
        {
            return null;
        }

        double size = bytes.Value;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{size:F1} {units[unitIndex]}");
    }

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")]
        List<OllamaModel>? Models);

    private sealed record OllamaModel(
        [property: JsonPropertyName("name")]
        string? Name,
        [property: JsonPropertyName("size")]
        long? Size);
}
