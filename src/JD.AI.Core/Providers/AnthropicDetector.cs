using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Anthropic API availability via API key.
/// Uses OpenAI-compatible connector with Anthropic's API endpoint.
/// </summary>
public sealed class AnthropicDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _apiKey;

    private static readonly ProviderModelInfo[] KnownModels =
    [
        new("claude-opus-4-20250514", "Claude Opus 4", "Anthropic"),
        new("claude-sonnet-4-20250514", "Claude Sonnet 4", "Anthropic"),
        new("claude-3-7-sonnet-20250219", "Claude 3.7 Sonnet", "Anthropic"),
        new("claude-3-5-haiku-20241022", "Claude 3.5 Haiku", "Anthropic"),
        new("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet v2", "Anthropic"),
    ];

    public AnthropicDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "Anthropic";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync("anthropic", "apikey", ct)
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

#pragma warning disable SKEXP0010
        builder.AddOpenAIChatCompletion(
            modelId: model.Id,
            apiKey: _apiKey ?? throw new InvalidOperationException("Anthropic API key not available."),
            httpClient: new HttpClient
            {
                BaseAddress = new Uri("https://api.anthropic.com/v1/"),
                Timeout = TimeSpan.FromMinutes(10),
            });
#pragma warning restore SKEXP0010

        return builder.Build();
    }
}
