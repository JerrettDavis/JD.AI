using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects HuggingFace Inference API availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.HuggingFace package.
/// </summary>
public sealed class HuggingFaceDetector : ApiKeyProviderDetectorBase
{
    private static readonly ProviderModelInfo[] KnownModelsCatalog =
    [
        new("meta-llama/Llama-3.3-70B-Instruct", "Llama 3.3 70B", "HuggingFace"),
        new("meta-llama/Llama-3.1-8B-Instruct", "Llama 3.1 8B", "HuggingFace"),
        new("mistralai/Mixtral-8x7B-Instruct-v0.1", "Mixtral 8x7B", "HuggingFace"),
        new("microsoft/Phi-3-mini-4k-instruct", "Phi-3 Mini", "HuggingFace"),
        new("Qwen/Qwen2.5-72B-Instruct", "Qwen 2.5 72B", "HuggingFace"),
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
