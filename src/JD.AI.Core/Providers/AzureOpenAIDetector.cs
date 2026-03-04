using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects Azure OpenAI availability via API key and endpoint.
/// Requires apikey, endpoint, and optional comma-separated deployments list.
/// </summary>
public sealed class AzureOpenAIDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _apiKey;
    private string? _endpoint;

    public AzureOpenAIDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "Azure OpenAI";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync("azure-openai", "apikey", ct)
            .ConfigureAwait(false);
        _endpoint = await _config.GetCredentialAsync("azure-openai", "endpoint", ct)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint))
        {
            return new ProviderInfo(ProviderName, IsAvailable: false,
                StatusMessage: "API key or endpoint not configured", Models: []);
        }

        // Get configured deployment names (comma-separated)
        var deploymentsRaw = await _config.GetCredentialAsync("azure-openai", "deployments", ct)
            .ConfigureAwait(false);

        List<ProviderModelInfo> models;
        if (!string.IsNullOrEmpty(deploymentsRaw))
        {
            models = deploymentsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => new ProviderModelInfo(d, $"azure:{d}", ProviderName))
                .ToList();
        }
        else
        {
            // Default deployments when none specified
            models =
            [
                new("gpt-4o", "azure:gpt-4o", ProviderName),
                new("gpt-4o-mini", "azure:gpt-4o-mini", ProviderName),
                new("gpt-4", "azure:gpt-4", ProviderName),
            ];
        }

        return new ProviderInfo(ProviderName, IsAvailable: true,
            StatusMessage: $"Authenticated - {models.Count} deployment(s)",
            Models: models);
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010
        builder.AddAzureOpenAIChatCompletion(
            deploymentName: model.Id,
            endpoint: _endpoint ?? throw new InvalidOperationException("Azure OpenAI endpoint not configured."),
            apiKey: _apiKey ?? throw new InvalidOperationException("Azure OpenAI API key not configured."));
#pragma warning restore SKEXP0010

        return builder.Build();
    }
}
