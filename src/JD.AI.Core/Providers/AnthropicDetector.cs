using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Anthropic.SDK;
using JD.AI.Core.Providers.Credentials;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Anthropic API availability via API key.
/// Dynamically discovers models from the Anthropic API, falling back to a
/// curated catalog of well-known Claude models.
/// </summary>
public sealed class AnthropicDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("claude-opus-4-20250514", "Claude Opus 4", "Anthropic"),
        new("claude-sonnet-4-20250514", "Claude Sonnet 4", "Anthropic"),
        new("claude-3-7-sonnet-20250219", "Claude 3.7 Sonnet", "Anthropic"),
        new("claude-3-5-haiku-20241022", "Claude 3.5 Haiku", "Anthropic"),
        new("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet v2", "Anthropic"),
    ];

    public AnthropicDetector(ProviderConfigurationManager config)
        : base(config, providerName: "Anthropic", providerKey: "anthropic")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override async Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await http
            .GetFromJsonAsync<AnthropicModelsResponse>(
                "https://api.anthropic.com/v1/models?limit=20", ct)
            .ConfigureAwait(false);

        if (response?.Data is not { Count: > 0 })
            return KnownModels;

        return response.Data
            .Where(m => !string.IsNullOrEmpty(m.Id) && string.Equals(m.Type, "model", StringComparison.Ordinal))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new ProviderModelInfo(
                m.Id!, m.DisplayName ?? m.Id!, ProviderName))
            .ToList();
    }

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
        builder.Services.AddSingleton(new AnthropicClient(
            new APIAuthentication(apiKey),
            new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10),
            }));

        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var client = sp.GetRequiredService<AnthropicClient>();
            return new ChatClientBuilder(
                    new AnthropicPromptCachingChatClient(client.Messages))
                .ConfigureOptions(o => o.ModelId ??= model.Id)
                .Build();
        });

        builder.Services.AddSingleton<IChatCompletionService>(sp =>
            sp.GetRequiredService<IChatClient>().AsChatCompletionService(sp));
    }

    private sealed record AnthropicModelsResponse(
        [property: JsonPropertyName("data")] List<AnthropicModel>? Data);

    private sealed record AnthropicModel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("created_at")] string? CreatedAt);
}
