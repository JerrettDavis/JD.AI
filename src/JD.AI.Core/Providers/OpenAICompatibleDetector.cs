using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
///     Generic detector for OpenAI-compatible API endpoints.
///     Supports multiple named instances (Groq, Together, DeepSeek, etc.).
///     Each instance is stored as openai-compat:{alias} in the credential store.
/// </summary>
public sealed class OpenAICompatibleDetector : IProviderDetector
{
    private static readonly HttpClient SharedClient = new();
    private readonly ProviderConfigurationManager _config;
    private readonly Dictionary<string, InstanceConfig> _instances = new(StringComparer.OrdinalIgnoreCase);

    public OpenAICompatibleDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "OpenAI-Compatible";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _instances.Clear();
        var allModels = new List<ProviderModelInfo>();

        // Check for configured instances in the credential store
        var keys = await _config.Store.ListKeysAsync("jdai:provider:openai-compat:", ct).ConfigureAwait(false);

        // Extract unique alias names from keys like jdai:provider:openai-compat:{alias}:{field}
        var aliases = keys.Select(k =>
            {
                var parts = k.Split(':');
                return parts.Length >= 4 ? parts[3] : null;
            }).
            Where(a => a != null).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList();

        // Also check for well-known compatible providers via env vars
        var wellKnown = new Dictionary<string, (string envKey, string baseUrl)>(StringComparer.OrdinalIgnoreCase)
        {
            ["groq"] = ("GROQ_API_KEY", "https://api.groq.com/openai/v1"),
            ["together"] = ("TOGETHER_API_KEY", "https://api.together.xyz/v1"),
            ["deepseek"] = ("DEEPSEEK_API_KEY", "https://api.deepseek.com/v1"),
            ["fireworks"] = ("FIREWORKS_API_KEY", "https://api.fireworks.ai/inference/v1"),
            ["perplexity"] = ("PERPLEXITY_API_KEY", "https://api.perplexity.ai")
        };

        foreach (var (alias, (envKey, baseUrl)) in wellKnown)
        {
            if (aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                continue; // Already configured via credential store

            var apiKey = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(apiKey))
            {
                aliases.Add(alias);
                _instances[alias] = new InstanceConfig(apiKey, baseUrl, alias);
            }
        }

        foreach (var alias in aliases)
        {
            if (_instances.ContainsKey(alias!))
                continue; // Already loaded from env vars

            var apiKey = await _config.GetCredentialAsync($"openai-compat:{alias}", "apikey", ct).ConfigureAwait(false);
            var baseUrl = await _config.GetCredentialAsync($"openai-compat:{alias}", "baseurl", ct).
                ConfigureAwait(false);

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(baseUrl))
                continue;

            _instances[alias!] = new InstanceConfig(apiKey, baseUrl, alias!);
        }

        // Try to discover models from each instance
        foreach (var (alias, instance) in _instances)
        {
            var models = await DiscoverModelsAsync(alias, instance, ct).ConfigureAwait(false);
            allModels.AddRange(models);
        }

        if (_instances.Count == 0)
            return new ProviderInfo(ProviderName,
                false,
                "No compatible endpoints configured",
                []);

        return new ProviderInfo(ProviderName,
            true,
            $"{_instances.Count} endpoint(s) - {allModels.Count} model(s)",
            allModels);
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        // Model ID format: {alias}/{modelId}
        var separatorIndex = model.Id.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex < 0)
            throw new InvalidOperationException($"Invalid OpenAI-compatible model ID: {model.Id}");

        var alias = model.Id[..separatorIndex];
        var modelId = model.Id[(separatorIndex + 1)..];

        if (!_instances.TryGetValue(alias, out var instance))
            throw new InvalidOperationException($"No OpenAI-compatible endpoint configured for '{alias}'.");

        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010
        builder.AddOpenAIChatCompletion(
            modelId,
            instance.ApiKey,
            httpClient: new HttpClient
            {
                BaseAddress = new Uri(instance.BaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromMinutes(10)
            });
#pragma warning restore SKEXP0010

        return builder.Build();
    }

    private static async Task<List<ProviderModelInfo>> DiscoverModelsAsync(
        string alias,
        InstanceConfig instance,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{instance.BaseUrl.TrimEnd('/')}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", instance.ApiKey);

            using var resp = await SharedClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return GetFallbackModels(alias);

            var body = await resp.Content.ReadFromJsonAsync<ModelsResponse>(ct).ConfigureAwait(false);

            return (body?.Data ?? []).Where(m => m.Id != null).
                Select(m => new ProviderModelInfo(
                    $"{alias}/{m.Id!}",
                    $"[{alias}] {m.Id}",
                    "OpenAI-Compatible")).
                ToList();
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            return GetFallbackModels(alias);
        }
    }

    private static List<ProviderModelInfo> GetFallbackModels(string alias)
    {
        // Return empty if we can't discover - user can still type model names
        return [];
    }

    private sealed record InstanceConfig(string ApiKey, string BaseUrl, string Alias);

    private sealed record ModelsResponse(
        [property: JsonPropertyName("data")] List<ModelEntry>? Data);

    private sealed record ModelEntry(
        [property: JsonPropertyName("id")] string? Id);
}
