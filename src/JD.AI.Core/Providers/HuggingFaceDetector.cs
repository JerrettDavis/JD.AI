using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects HuggingFace Inference API availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.HuggingFace package.
/// </summary>
public sealed class HuggingFaceDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _apiKey;

    private static readonly ProviderModelInfo[] KnownModels =
    [
        new("meta-llama/Llama-3.3-70B-Instruct", "Llama 3.3 70B", "HuggingFace"),
        new("meta-llama/Llama-3.1-8B-Instruct", "Llama 3.1 8B", "HuggingFace"),
        new("mistralai/Mixtral-8x7B-Instruct-v0.1", "Mixtral 8x7B", "HuggingFace"),
        new("microsoft/Phi-3-mini-4k-instruct", "Phi-3 Mini", "HuggingFace"),
        new("Qwen/Qwen2.5-72B-Instruct", "Qwen 2.5 72B", "HuggingFace"),
    ];

    public HuggingFaceDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "HuggingFace";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync("huggingface", "apikey", ct)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(_apiKey))
        {
            return new ProviderInfo(ProviderName, IsAvailable: false,
                StatusMessage: "No API key configured", Models: []);
        }

        return new ProviderInfo(ProviderName, IsAvailable: true,
            StatusMessage: $"Authenticated - {KnownModels.Length} model(s)",
            Models: KnownModels.ToList());
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070
        builder.AddHuggingFaceChatCompletion(
            model: model.Id,
            apiKey: _apiKey ?? throw new InvalidOperationException("HuggingFace API key not available."));
#pragma warning restore SKEXP0070

        return builder.Build();
    }
}
