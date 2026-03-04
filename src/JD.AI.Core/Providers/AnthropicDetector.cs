using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Anthropic API availability via API key.
/// Uses OpenAI-compatible connector with Anthropic's API endpoint.
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

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0010
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: apiKey,
            httpClient: new HttpClient
            {
                BaseAddress = new Uri("https://api.anthropic.com/v1/"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010
    }
}
