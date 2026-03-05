using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects HuggingFace Inference API availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.HuggingFace package.
/// </summary>
/// <remarks>
/// Model catalog targets the HuggingFace serverless Inference API.
/// Large models (70B+) may not be available on the free tier and can
/// return HTTP 410 Gone — prefer smaller inference-ready models here.
/// Use <c>/model search</c> to discover additional models dynamically.
/// </remarks>
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

    protected override void ConfigureKernel(IKernelBuilder builder, ProviderModelInfo model, string apiKey)
    {
#pragma warning disable SKEXP0070
        builder.AddHuggingFaceChatCompletion(
            model: model.Id,
            apiKey: apiKey);
#pragma warning restore SKEXP0070
    }
}
