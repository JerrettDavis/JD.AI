using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Mistral AI availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.MistralAI package.
/// </summary>
public sealed class MistralDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _apiKey;

    private static readonly ProviderModelInfo[] KnownModels =
    [
        new("mistral-large-latest", "Mistral Large", "Mistral"),
        new("mistral-medium-latest", "Mistral Medium", "Mistral"),
        new("mistral-small-latest", "Mistral Small", "Mistral"),
        new("codestral-latest", "Codestral", "Mistral"),
        new("open-mistral-nemo", "Mistral Nemo", "Mistral"),
        new("ministral-8b-latest", "Ministral 8B", "Mistral"),
    ];

    public MistralDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "Mistral";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync("mistral", "apikey", ct)
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
        builder.AddMistralChatCompletion(
            modelId: model.Id,
            apiKey: _apiKey ?? throw new InvalidOperationException("Mistral API key not available."));
#pragma warning restore SKEXP0070

        return builder.Build();
    }
}
