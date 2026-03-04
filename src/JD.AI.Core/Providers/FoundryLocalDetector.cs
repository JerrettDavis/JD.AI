using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a locally-running Microsoft Foundry Local instance and enumerates
/// its models via the OpenAI-compatible REST API.
/// </summary>
public sealed class FoundryLocalDetector : IProviderDetector
{
    private readonly string _endpoint;
    private static readonly HttpClient SharedClient = new();

    public FoundryLocalDetector(string endpoint = "http://127.0.0.1:64646")
    {
        _endpoint = endpoint.TrimEnd('/');
    }

    public string ProviderName => "Foundry Local";

    /// <summary>The resolved endpoint (trailing slash stripped).</summary>
    internal string Endpoint => _endpoint;

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await SharedClient
                .GetFromJsonAsync<FoundryModelsResponse>(
                    $"{_endpoint}/v1/models", ct)
                .ConfigureAwait(false);

            var models = (resp?.Data ?? [])
                .Select(m =>
                {
                    var name = m.Id ?? "unknown";
                    var caps = ModelCapabilityHeuristics.InferFromName(name);
                    return new ProviderModelInfo(name, name, ProviderName, Capabilities: caps);
                })
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
            apiKey: "foundry",
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{_endpoint}/v1/"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010

        return builder.Build();
    }

    private sealed record FoundryModelsResponse(
        [property: JsonPropertyName("data")]
        List<FoundryModel>? Data);

    private sealed record FoundryModel(
        [property: JsonPropertyName("id")]
        string? Id);
}
