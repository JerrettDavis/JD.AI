using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Mistral AI availability via API key.
/// Dynamically discovers models from the Mistral API, falling back to a
/// curated catalog when the endpoint is unreachable.
/// </summary>
public sealed class MistralDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("mistral-large-latest", "Mistral Large", "Mistral",
            Capabilities: ModelCapabilityHeuristics.InferFromName("mistral-large-latest")),
        new("mistral-medium-latest", "Mistral Medium", "Mistral",
            Capabilities: ModelCapabilityHeuristics.InferFromName("mistral-medium-latest")),
        new("mistral-small-latest", "Mistral Small", "Mistral",
            Capabilities: ModelCapabilityHeuristics.InferFromName("mistral-small-latest")),
        new("codestral-latest", "Codestral", "Mistral",
            Capabilities: ModelCapabilityHeuristics.InferFromName("codestral-latest")),
        new("open-mistral-nemo", "Mistral Nemo", "Mistral",
            Capabilities: ModelCapabilityHeuristics.InferFromName("open-mistral-nemo")),
        new("ministral-8b-latest", "Ministral 8B", "Mistral",
            Capabilities: ModelCapabilityHeuristics.InferFromName("ministral-8b-latest")),
    ];

    public MistralDetector(ProviderConfigurationManager config)
        : base(config, providerName: "Mistral", providerKey: "mistral")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await http
            .GetFromJsonAsync<MistralModelsResponse>("https://api.mistral.ai/v1/models", ct)
            .ConfigureAwait(false);

        if (response?.Data is not { Count: > 0 })
            return KnownModels;

        return response.Data
            .Where(m => !string.IsNullOrEmpty(m.Id) && IsConversationalModelId(m.Id!))
            .OrderByDescending(m => m.Created)
            .Select(m => new ProviderModelInfo(
                m.Id!,
                FormatName(m.Id!),
                ProviderName,
                Capabilities: ModelCapabilityHeuristics.InferFromName(m.Id!)))
            .ToList();
    }

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0070
        builder.AddMistralChatCompletion(
            modelId: model.Id,
            apiKey: apiKey);
#pragma warning restore SKEXP0070
    }

    private static string FormatName(string id) =>
        id.Replace('-', ' ')
          .Replace("latest", "", StringComparison.OrdinalIgnoreCase)
          .Trim();

    // Exclude non-conversational catalogs from fallback/routing.
    internal static bool IsConversationalModelId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return !(
            id.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("moderation", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("ocr", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record MistralModelsResponse(
        [property: JsonPropertyName("data")] List<MistralModel>? Data);

    private sealed record MistralModel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("created")] long Created);
}
