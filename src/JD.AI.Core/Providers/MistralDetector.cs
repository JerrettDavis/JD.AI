using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Mistral AI availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.MistralAI package.
/// </summary>
public sealed class MistralDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("mistral-large-latest", "Mistral Large", "Mistral"),
        new("mistral-medium-latest", "Mistral Medium", "Mistral"),
        new("mistral-small-latest", "Mistral Small", "Mistral"),
        new("codestral-latest", "Codestral", "Mistral"),
        new("open-mistral-nemo", "Mistral Nemo", "Mistral"),
        new("ministral-8b-latest", "Ministral 8B", "Mistral"),
    ];

    public MistralDetector(ProviderConfigurationManager config)
        : base(config, providerName: "Mistral", providerKey: "mistral")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0070
        builder.AddMistralChatCompletion(
            modelId: model.Id,
            apiKey: apiKey);
#pragma warning restore SKEXP0070
    }
}
