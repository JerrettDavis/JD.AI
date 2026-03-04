using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Google Gemini API availability via API key.
/// Uses the official Microsoft.SemanticKernel.Connectors.Google package.
/// </summary>
public sealed class GoogleGeminiDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _apiKey;

    private static readonly ProviderModelInfo[] KnownModels =
    [
        new("gemini-2.5-pro", "Gemini 2.5 Pro", "Google Gemini"),
        new("gemini-2.5-flash", "Gemini 2.5 Flash", "Google Gemini"),
        new("gemini-2.0-flash", "Gemini 2.0 Flash", "Google Gemini"),
        new("gemini-1.5-pro", "Gemini 1.5 Pro", "Google Gemini"),
        new("gemini-1.5-flash", "Gemini 1.5 Flash", "Google Gemini"),
    ];

    public GoogleGeminiDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "Google Gemini";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync("google-gemini", "apikey", ct)
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
        builder.AddGoogleAIGeminiChatCompletion(
            modelId: model.Id,
            apiKey: _apiKey ?? throw new InvalidOperationException("Google Gemini API key not available."));
#pragma warning restore SKEXP0070

        return builder.Build();
    }
}
