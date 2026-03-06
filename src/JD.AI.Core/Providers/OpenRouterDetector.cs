using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// First-class provider for OpenRouter — a unified API that exposes hundreds of
/// AI models from multiple vendors through an OpenAI-compatible interface.
/// Dynamically discovers models from the OpenRouter catalog, enriching each with
/// context length, pricing, and capability metadata.
/// </summary>
public sealed class OpenRouterDetector : ApiKeyProviderDetectorBase
{
    private const string BaseUrl = "https://openrouter.ai/api/v1";

    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("anthropic/claude-sonnet-4", "Claude Sonnet 4", "OpenRouter",
            ContextWindowTokens: 200_000, MaxOutputTokens: 16_384),
        new("openai/gpt-4.1", "GPT-4.1", "OpenRouter",
            ContextWindowTokens: 1_047_576, MaxOutputTokens: 32_768),
        new("google/gemini-2.5-pro", "Gemini 2.5 Pro", "OpenRouter",
            ContextWindowTokens: 1_048_576, MaxOutputTokens: 65_536),
        new("meta-llama/llama-4-maverick", "Llama 4 Maverick", "OpenRouter",
            ContextWindowTokens: 1_048_576, MaxOutputTokens: 1_048_576),
        new("mistralai/mistral-large", "Mistral Large", "OpenRouter",
            ContextWindowTokens: 128_000, MaxOutputTokens: 128_000),
    ];

    public OpenRouterDetector(ProviderConfigurationManager config)
        : base(config, providerName: "OpenRouter", providerKey: "openrouter")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await http
            .GetFromJsonAsync<OpenRouterModelsResponse>($"{BaseUrl}/models", ct)
            .ConfigureAwait(false);

        if (response?.Data is not { Count: > 0 })
            return KnownModels;

        return response.Data
            .Where(m => !string.IsNullOrEmpty(m.Id)
                        // Include models with no architecture info; exclude only those that
                        // explicitly declare output modalities without "text" (e.g. image-only).
                        && m.Architecture?.OutputModalities?.Contains("text") != false)
            .Select(m =>
            {
                var caps = ModelCapabilities.Chat;
                if (m.SupportedParameters?.Contains("tools") == true
                    || m.SupportedParameters?.Contains("tool_choice") == true)
                    caps |= ModelCapabilities.ToolCalling;
                if (m.Architecture?.InputModalities?.Contains("image") == true)
                    caps |= ModelCapabilities.Vision;

                return new ProviderModelInfo(
                    m.Id!,
                    m.Name ?? m.Id!,
                    ProviderName,
                    ContextWindowTokens: m.ContextLength ?? 128_000,
                    MaxOutputTokens: m.TopProvider?.MaxCompletionTokens ?? 16_384,
                    InputCostPerToken: ParseDecimal(m.Pricing?.Prompt),
                    OutputCostPerToken: ParseDecimal(m.Pricing?.Completion),
                    HasMetadata: true,
                    Capabilities: caps);
            })
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected override void ConfigureKernel(
        IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0010
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: apiKey,
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{BaseUrl}/"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

    // --- API response DTOs ---

    private sealed record OpenRouterModelsResponse(
        [property: JsonPropertyName("data")] List<OpenRouterModel>? Data);

    private sealed record OpenRouterModel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("context_length")] int? ContextLength,
        [property: JsonPropertyName("pricing")] OpenRouterPricing? Pricing,
        [property: JsonPropertyName("architecture")] OpenRouterArchitecture? Architecture,
        [property: JsonPropertyName("top_provider")] OpenRouterTopProvider? TopProvider,
        [property: JsonPropertyName("supported_parameters")] List<string>? SupportedParameters);

    private sealed record OpenRouterPricing(
        [property: JsonPropertyName("prompt")] string? Prompt,
        [property: JsonPropertyName("completion")] string? Completion);

    private sealed record OpenRouterArchitecture(
        [property: JsonPropertyName("input_modalities")] List<string>? InputModalities,
        [property: JsonPropertyName("output_modalities")] List<string>? OutputModalities);

    private sealed record OpenRouterTopProvider(
        [property: JsonPropertyName("max_completion_tokens")] int? MaxCompletionTokens);
}
