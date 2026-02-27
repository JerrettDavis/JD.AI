using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Providers;

/// <summary>
/// Detects a locally-running Ollama instance and enumerates its models.
/// </summary>
public sealed class OllamaDetector : IProviderDetector
{
    private readonly string _endpoint;
    private static readonly HttpClient SharedClient = new();

    public OllamaDetector(string endpoint = "http://localhost:11434")
    {
        _endpoint = endpoint.TrimEnd('/');
    }

    public string ProviderName => "Ollama";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await SharedClient
                .GetFromJsonAsync<OllamaTagsResponse>(
                    $"{_endpoint}/api/tags", ct)
                .ConfigureAwait(false);

            var models = (resp?.Models ?? [])
                .Select(m => new ProviderModelInfo(
                    m.Name ?? "unknown",
                    m.Name ?? "unknown",
                    ProviderName))
                .ToList();

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"{models.Count} model(s) available",
                Models: models);
        }
        catch (HttpRequestException)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: "Not running",
                Models: []);
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010 // OpenAI connector experimental
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: "ollama",
            httpClient: new HttpClient { BaseAddress = new Uri($"{_endpoint}/v1") });
#pragma warning restore SKEXP0010

        return builder.Build();
    }

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")]
        List<OllamaModel>? Models);

    private sealed record OllamaModel(
        [property: JsonPropertyName("name")]
        string? Name);
}
