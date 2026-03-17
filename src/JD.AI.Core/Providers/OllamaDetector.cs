using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Ollama instances (local and optionally named remote endpoints) and enumerates models.
/// </summary>
public sealed class OllamaDetector : IProviderDetector
{
    private readonly string _defaultEndpoint;
    private readonly ProviderConfigurationManager? _config;
    private static readonly HttpClient SharedClient = new();
    private static readonly ConcurrentDictionary<string, ModelCapabilities> CapabilityCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OllamaInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

    public OllamaDetector(string endpoint = "http://localhost:11434")
    {
        _defaultEndpoint = endpoint.TrimEnd('/');
    }

    public OllamaDetector(
        ProviderConfigurationManager config,
        string defaultEndpoint = "http://localhost:11434")
    {
        _config = config;
        _defaultEndpoint = defaultEndpoint.TrimEnd('/');
    }

    public string ProviderName => "Ollama";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        await LoadInstancesAsync(ct).ConfigureAwait(false);

        var allModels = new List<ProviderModelInfo>();
        var onlineEndpoints = 0;

        foreach (var instance in _instances.Values)
        {
            try
            {
                var resp = await SharedClient
                    .GetFromJsonAsync<OllamaTagsResponse>(
                        $"{instance.Endpoint}/api/tags", ct)
                    .ConfigureAwait(false);

                var rawModels = resp?.Models ?? [];
                var tasks = rawModels.Select(async m =>
                {
                    var name = m.Name ?? "unknown";
                    var caps = await ProbeCapabilitiesAsync(instance.Endpoint, name, ct).ConfigureAwait(false);

                    var modelId = instance.IsDefault
                        ? name
                        : $"{instance.Alias}/{name}";
                    var displayName = instance.IsDefault
                        ? name
                        : $"[{instance.Alias}] {name}";

                    return new ProviderModelInfo(modelId, displayName, ProviderName, Capabilities: caps);
                });

                allModels.AddRange(await Task.WhenAll(tasks).ConfigureAwait(false));
                onlineEndpoints++;
            }
            catch (HttpRequestException) when (!ct.IsCancellationRequested)
            {
                // Keep probing other configured endpoints.
            }
        }

        if (onlineEndpoints == 0)
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: "Not running",
                Models: []);
        }

        return new ProviderInfo(
            ProviderName,
            IsAvailable: true,
            StatusMessage: $"{onlineEndpoints}/{_instances.Count} endpoint(s) online - {allModels.Count} model(s)",
            Models: allModels);
    }

    /// <summary>
    /// Probes a model's capabilities by calling /api/show and inspecting
    /// the template for tool-calling tokens. Falls back to name heuristics.
    /// </summary>
    internal async Task<ModelCapabilities> ProbeCapabilitiesAsync(
        string endpoint,
        string modelName,
        CancellationToken ct)
    {
        var cacheKey = $"{endpoint}|{modelName}";
        if (CapabilityCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var caps = ModelCapabilities.Chat;

        try
        {
            var showResp = await SharedClient
                .PostAsJsonAsync($"{endpoint}/api/show", new { name = modelName }, ct)
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

        CapabilityCache[cacheKey] = caps;
        return caps;
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        EnsureInstancesLoadedForBuild();

        var (alias, modelId) = ParseModelIdentifier(model.Id);
        if (!_instances.TryGetValue(alias, out var instance))
            throw new InvalidOperationException($"No Ollama endpoint configured for '{alias}'.");

        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010 // OpenAI connector experimental
        builder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: "ollama",
            httpClient: new HttpClient
            {
                BaseAddress = new Uri($"{instance.Endpoint}/v1"),
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

    private sealed record OllamaInstance(string Alias, string Endpoint, bool IsDefault);

    private async Task LoadInstancesAsync(CancellationToken ct)
    {
        _instances.Clear();
        _instances["local"] = new OllamaInstance("local", _defaultEndpoint, IsDefault: true);

        if (_config is null)
            return;

        var keys = await _config.Store
            .ListKeysAsync("jdai:provider:ollama:", ct)
            .ConfigureAwait(false);

        var aliases = keys
            .Select(key =>
            {
                // key format: jdai:provider:ollama:{alias}:{field}
                var parts = key.Split(':');
                return parts.Length >= 5 ? parts[3] : null;
            })
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var alias in aliases)
        {
            var endpoint = await _config
                .GetCredentialAsync($"ollama:{alias}", "endpoint", ct)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = await _config
                    .GetCredentialAsync($"ollama:{alias}", "baseurl", ct)
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(endpoint))
                continue;

            var trimmedAlias = alias!.Trim();
            var normalizedEndpoint = endpoint.TrimEnd('/');
            var isDefault = string.Equals(trimmedAlias, "local", StringComparison.OrdinalIgnoreCase);
            _instances[trimmedAlias] = new OllamaInstance(trimmedAlias, normalizedEndpoint, isDefault);
        }
    }

    private void EnsureInstancesLoadedForBuild()
    {
        if (_instances.Count > 0)
            return;

        if (_config is null)
        {
            _instances["local"] = new OllamaInstance("local", _defaultEndpoint, IsDefault: true);
            return;
        }

        try
        {
            LoadInstancesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort fallback for kernel construction.
            _instances.Clear();
            _instances["local"] = new OllamaInstance("local", _defaultEndpoint, IsDefault: true);
        }
    }

    private (string Alias, string ModelId) ParseModelIdentifier(string modelId)
    {
        var idx = modelId.IndexOf('/', StringComparison.Ordinal);
        if (idx <= 0)
            return ("local", modelId);

        var alias = modelId[..idx];
        var actualModelId = modelId[(idx + 1)..];

        if (_instances.ContainsKey(alias))
            return (alias, actualModelId);

        // If the prefix is not a configured alias, treat the entire value as a local model ID.
        // This preserves compatibility with model names that contain '/'.
        return ("local", modelId);
    }
}
