using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Google Gemini API availability via API key.
/// Dynamically discovers models from the Generative Language API,
/// filtering to <c>generateContent</c>-capable models.
/// </summary>
public sealed class GoogleGeminiDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("gemini-2.5-pro", "Gemini 2.5 Pro", "Google Gemini"),
        new("gemini-2.5-flash", "Gemini 2.5 Flash", "Google Gemini"),
        new("gemini-2.0-flash", "Gemini 2.0 Flash", "Google Gemini"),
        new("gemini-1.5-pro", "Gemini 1.5 Pro", "Google Gemini"),
        new("gemini-1.5-flash", "Gemini 1.5 Flash", "Google Gemini"),
    ];

    public GoogleGeminiDetector(ProviderConfigurationManager config)
        : base(config, providerName: "Google Gemini", providerKey: "google-gemini")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}";
        var response = await http.GetFromJsonAsync<GeminiModelsResponse>(url, ct)
            .ConfigureAwait(false);

        if (response?.Models is not { Count: > 0 })
            return KnownModels;

        return response.Models
            .Where(m => !string.IsNullOrEmpty(m.Name)
                        && m.SupportedMethods?.Contains("generateContent") == true)
            .Select(m =>
            {
                // API returns "models/gemini-2.5-pro" — strip prefix
                var id = m.Name!.StartsWith("models/", StringComparison.Ordinal)
                    ? m.Name["models/".Length..]
                    : m.Name;
                var display = m.DisplayName ?? id;
                return new ProviderModelInfo(id, display, ProviderName);
            })
            .ToList();
    }

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: model.Id,
            apiKey: apiKey);
#pragma warning restore SKEXP0070
    }

    private sealed record GeminiModelsResponse(
        [property: JsonPropertyName("models")] List<GeminiModel>? Models);

    private sealed record GeminiModel(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("supportedGenerationMethods")] List<string>? SupportedMethods);
}
