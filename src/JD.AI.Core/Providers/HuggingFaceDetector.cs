using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects HuggingFace Inference API availability via API key.
/// Dynamically discovers the most popular inference-ready chat models from
/// the HuggingFace Hub, falling back to a curated catalog when offline.
///
/// HuggingFace deprecated the old api-inference.huggingface.co endpoint (returns 410 Gone).
/// All requests now use the OpenAI-compatible router: https://router.huggingface.co/hf-inference/v1/
/// </summary>
public sealed class HuggingFaceDetector : ApiKeyProviderDetectorBase
{
    /// <summary>The new OpenAI-compatible HuggingFace Inference router endpoint.</summary>
    internal static readonly Uri InferenceEndpoint =
        new("https://router.huggingface.co/v1/");

    /// <summary>
    /// Fallback catalog of models confirmed available on the hf-inference provider.
    /// Only models with hf-inference support should be listed here.
    /// </summary>
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("Qwen/Qwen2.5-72B-Instruct", "Qwen 2.5 72B", "HuggingFace"),
        new("Qwen/Qwen2.5-7B-Instruct", "Qwen 2.5 7B", "HuggingFace"),
        new("meta-llama/Llama-3.3-70B-Instruct", "Llama 3.3 70B", "HuggingFace"),
        new("meta-llama/Llama-3.1-8B-Instruct", "Llama 3.1 8B", "HuggingFace"),
        new("mistralai/Mistral-7B-Instruct-v0.3", "Mistral 7B v0.3", "HuggingFace"),
        new("microsoft/Phi-4-mini-instruct", "Phi 4 Mini", "HuggingFace"),
        new("deepseek-ai/DeepSeek-R1-Distill-Qwen-32B", "DeepSeek R1 32B", "HuggingFace"),
        new("google/gemma-3-27b-it", "Gemma 3 27B", "HuggingFace"),
    ];

    public HuggingFaceDetector(ProviderConfigurationManager config)
        : base(config, providerName: "HuggingFace", providerKey: "huggingface")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Filter specifically for models hosted on the hf-inference provider (free, HF-hosted).
        // expand=inferenceProviderMapping lets us verify hf-inference is available per model.
        const string url = "https://huggingface.co/api/models?" +
                           "pipeline_tag=text-generation&" +
                           "inference_provider=hf-inference&" +
                           "sort=downloads&direction=-1&limit=20&" +
                           "expand=inferenceProviderMapping";

        var models = await http.GetFromJsonAsync<List<HfModelEntry>>(url, ct)
            .ConfigureAwait(false);

        if (models is null or { Count: 0 })
            return KnownModels;

        return models
            .Where(m => !string.IsNullOrEmpty(m.Id) && IsHfInferenceAvailable(m))
            .Take(10)
            .Select(m => new ProviderModelInfo(m.Id!, FormatName(m.Id!), ProviderName))
            .ToList<ProviderModelInfo>()
            is { Count: > 0 } discovered ? discovered : KnownModels;
    }

    /// <summary>
    /// Uses the OpenAI-compatible HuggingFace router endpoint instead of the deprecated
    /// api-inference.huggingface.co endpoint (which returns 410 Gone as of 2025).
    /// New endpoint: https://router.huggingface.co/v1/
    /// </summary>
    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0010
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: apiKey,
            endpoint: InferenceEndpoint);
#pragma warning restore SKEXP0010
    }

    private static bool IsHfInferenceAvailable(HfModelEntry m) =>
        m.InferenceProviderMapping is null ||
        m.InferenceProviderMapping.ContainsKey("hf-inference");

    private static string FormatName(string id)
    {
        var name = id.Contains('/') ? id[(id.IndexOf('/') + 1)..] : id;
        return name
            .Replace("-Instruct", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-it", "", StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ')
            .Trim();
    }

    private sealed record HfModelEntry(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("inferenceProviderMapping")]
        Dictionary<string, object>? InferenceProviderMapping);
}
