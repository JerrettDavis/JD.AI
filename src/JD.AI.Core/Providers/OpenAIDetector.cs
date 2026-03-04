using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects OpenAI API availability via API key and discovers models.
/// </summary>
public sealed class OpenAIDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _apiKey;
    private static readonly HttpClient SharedClient = new();

    public OpenAIDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "OpenAI";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync("openai", "apikey", ct)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(_apiKey))
        {
            return new ProviderInfo(ProviderName, IsAvailable: false,
                StatusMessage: "No API key configured", Models: []);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.openai.com/v1/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var resp = await SharedClient.SendAsync(request, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content
                .ReadFromJsonAsync<OpenAIModelsResponse>(ct)
                .ConfigureAwait(false);

            var chatModels = (body?.Data ?? [])
                .Where(m => m.Id != null && (
                    m.Id.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
                    m.Id.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
                    m.Id.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
                    m.Id.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
                    m.Id.StartsWith("chatgpt", StringComparison.OrdinalIgnoreCase)))
                .Select(m => new ProviderModelInfo(m.Id!, m.Id!, ProviderName))
                .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ProviderInfo(ProviderName, IsAvailable: true,
                StatusMessage: $"Authenticated - {chatModels.Count} model(s)",
                Models: chatModels);
        }
        catch (HttpRequestException ex)
        {
            return new ProviderInfo(ProviderName, IsAvailable: false,
                StatusMessage: $"API error: {ex.Message}", Models: []);
        }
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: _apiKey ?? throw new InvalidOperationException("OpenAI API key not available."));
        return builder.Build();
    }

    private sealed record OpenAIModelsResponse(
        [property: JsonPropertyName("data")]
        List<OpenAIModelEntry>? Data);

    private sealed record OpenAIModelEntry(
        [property: JsonPropertyName("id")]
        string? Id);
}
