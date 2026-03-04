using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects a locally-running Ollama instance and enumerates its models.
/// </summary>
public sealed class OllamaDetector : IProviderDetector
{
    private readonly string _endpoint;
    private static readonly HttpClient SharedClient = new();
    private static readonly ConcurrentDictionary<string, ModelCapabilities> CapabilityCache = new(StringComparer.OrdinalIgnoreCase);

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

            var rawModels = resp?.Models ?? [];

            // Probe capabilities in parallel (cached after first call)
            var tasks = rawModels
                .Select(async m =>
                {
                    var name = m.Name ?? "unknown";
                    var caps = await ProbeCapabilitiesAsync(name, ct).ConfigureAwait(false);
                    return new ProviderModelInfo(name, name, ProviderName, caps);
                });
            var models = await Task.WhenAll(tasks).ConfigureAwait(false);

            return new ProviderInfo(
                ProviderName,
                IsAvailable: true,
                StatusMessage: $"{models.Length} model(s) available",
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

    /// <summary>
    /// Probes a model's capabilities by calling /api/show and inspecting
    /// the template for tool-calling tokens. Falls back to name heuristics.
    /// </summary>
    internal async Task<ModelCapabilities> ProbeCapabilitiesAsync(string modelName, CancellationToken ct)
    {
        if (CapabilityCache.TryGetValue(modelName, out var cached))
            return cached;

        var caps = ModelCapabilities.Chat;

        try
        {
            var showResp = await SharedClient
                .PostAsJsonAsync($"{_endpoint}/api/show", new { name = modelName }, ct)
                .ConfigureAwait(false);

            if (showResp.IsSuccessStatusCode)
            {
                var show = await showResp.Content
                    .ReadFromJsonAsync<OllamaShowResponse>(ct)
                    .ConfigureAwait(false);

                if (show != null)
                {
                    // Check template for tool-calling tokens
                    var template = show.Template ?? string.Empty;
                    if (template.Contains("<tool_call>", StringComparison.OrdinalIgnoreCase)
                        || template.Contains("{{.ToolCalls}}", StringComparison.OrdinalIgnoreCase)
                        || template.Contains("<|tool_calls|>", StringComparison.OrdinalIgnoreCase)
                        || template.Contains("<function_call>", StringComparison.OrdinalIgnoreCase)
                        || template.Contains("tools", StringComparison.OrdinalIgnoreCase))
                    {
                        caps |= ModelCapabilities.ToolCalling;
                    }

                    // Check for vision projector in model info
                    var modelInfo = show.ModelInfo ?? string.Empty;
                    if (modelInfo.Contains("vision", StringComparison.OrdinalIgnoreCase)
                        || modelInfo.Contains("projector", StringComparison.OrdinalIgnoreCase))
                    {
                        caps |= ModelCapabilities.Vision;
                    }
                }
            }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Fall through to heuristics
        }

        // If /api/show didn't reveal tools, try name heuristics as fallback
        if (!caps.HasFlag(ModelCapabilities.ToolCalling))
        {
            var heuristic = ModelCapabilityHeuristics.InferFromName(modelName);
            caps |= heuristic & ~ModelCapabilities.Chat; // Merge non-Chat flags
        }

        CapabilityCache[modelName] = caps;
        return caps;
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010 // OpenAI connector experimental
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: "ollama",
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{_endpoint}/v1"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010

        return builder.Build();
    }

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")]
        List<OllamaModel>? Models);

    private sealed record OllamaModel(
        [property: JsonPropertyName("name")]
        string? Name);

    private sealed record OllamaShowResponse(
        [property: JsonPropertyName("template")]
        string? Template,
        [property: JsonPropertyName("modelinfo")]
        string? ModelInfo);
}
