using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects HuggingFace Inference API availability via API key.
/// Dynamically discovers the most popular inference-ready chat models from
/// the HuggingFace Hub, falling back to a curated catalog when offline.
/// </summary>
public sealed class HuggingFaceDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("Qwen/Qwen2.5-7B-Instruct", "Qwen 2.5 7B", "HuggingFace"),
        new("meta-llama/Llama-3.1-8B-Instruct", "Llama 3.1 8B", "HuggingFace"),
        new("Qwen/Qwen3-8B", "Qwen 3 8B", "HuggingFace"),
        new("mistralai/Mistral-7B-Instruct-v0.3", "Mistral 7B v0.3", "HuggingFace"),
        new("microsoft/Phi-3.5-mini-instruct", "Phi 3.5 Mini", "HuggingFace"),
    ];

    public HuggingFaceDetector(ProviderConfigurationManager config)
        : base(config, providerName: "HuggingFace", providerKey: "huggingface")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        const string url = "https://huggingface.co/api/models?" +
                           "pipeline_tag=text-generation&" +
                           "inference_provider=all&" +
                           "sort=downloads&direction=-1&limit=10&" +
                           "filter=conversational";

        var models = await http.GetFromJsonAsync<List<HfModelEntry>>(url, ct)
            .ConfigureAwait(false);

        if (models is null or { Count: 0 })
            return KnownModels;

        return models
            .Where(m => !string.IsNullOrEmpty(m.Id))
            .Select(m => new ProviderModelInfo(m.Id!, FormatName(m.Id!), ProviderName))
            .ToList();
    }

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0070
        builder.AddHuggingFaceChatCompletion(
            model: model.Id,
            apiKey: apiKey);
#pragma warning restore SKEXP0070
    }

    private static string FormatName(string id)
    {
        var name = id.Contains('/') ? id[(id.IndexOf('/') + 1)..] : id;
        return name
            .Replace("-Instruct", "", StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ')
            .Trim();
    }

    private sealed record HfModelEntry(
        [property: JsonPropertyName("id")] string? Id);
}
