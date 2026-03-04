using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Google Gemini API availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.Google package.
/// </summary>
public sealed class GoogleGeminiDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("gemini-2.5-pro", "Gemini 2.5 Pro", "Google Gemini"),
        new("gemini-2.5-flash", "Gemini 2.5 Flash", "Google Gemini"),
        new("gemini-2.0-flash", "Gemini 2.0 Flash", "Google Gemini"),
        new("gemini-1.5-pro", "Gemini 1.5 Pro", "Google Gemini"),
        new("gemini-1.5-flash", "Gemini 1.5 Flash", "Google Gemini"),
    ];

    public GoogleGeminiDetector(ProviderConfigurationManager config)
        : base(config, providerName: "Google Gemini", providerKey: "google-gemini")
    {
    }

    protected override IReadOnlyList<ProviderModelInfo> KnownModels => KnownModelsCatalog;

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0070
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: model.Id,
            apiKey: apiKey);
#pragma warning restore SKEXP0070
    }
}
