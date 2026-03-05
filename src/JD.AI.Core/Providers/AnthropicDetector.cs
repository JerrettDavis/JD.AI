using Anthropic.SDK;
using JD.AI.Core.Providers.Credentials;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Anthropic API availability via API key.
/// Uses Anthropic's native messages API through the Anthropic SDK.
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
}
