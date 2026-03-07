using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Detects AWS Bedrock availability via AWS credentials.
/// Uses the official Microsoft.SemanticKernel.Connectors.Amazon package.
/// </summary>
public sealed class AmazonBedrockDetector : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private string? _region;

    private static readonly ProviderModelInfo[] KnownModels =
    [
        new("anthropic.claude-sonnet-4-20250514-v1:0", "Claude Sonnet 4 (Bedrock)", "AWS Bedrock"),
        new("anthropic.claude-3-5-sonnet-20241022-v2:0", "Claude 3.5 Sonnet v2 (Bedrock)", "AWS Bedrock"),
        new("anthropic.claude-3-5-haiku-20241022-v1:0", "Claude 3.5 Haiku (Bedrock)", "AWS Bedrock"),
        new("amazon.nova-pro-v1:0", "Amazon Nova Pro", "AWS Bedrock"),
        new("amazon.nova-lite-v1:0", "Amazon Nova Lite", "AWS Bedrock"),
        new("meta.llama3-1-70b-instruct-v1:0", "Llama 3.1 70B (Bedrock)", "AWS Bedrock"),
    ];

    public AmazonBedrockDetector(ProviderConfigurationManager config)
    {
        _config = config;
    }

    public string ProviderName => "AWS Bedrock";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        var accessKey = await _config.GetCredentialAsync("bedrock", "accesskey", ct)
            .ConfigureAwait(false);
        var secretKey = await _config.GetCredentialAsync("bedrock", "secretkey", ct)
            .ConfigureAwait(false);
        _region = await _config.GetCredentialAsync("bedrock", "region", ct)
            .ConfigureAwait(false) ?? "us-east-1";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            return new ProviderInfo(ProviderName, IsAvailable: false,
                StatusMessage: "AWS credentials not configured", Models: []);
        }

        // Set AWS env vars for the SDK to pick up (if not already set)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")))
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", accessKey);
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")))
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", secretKey);
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_REGION")))
            Environment.SetEnvironmentVariable("AWS_REGION", _region);

        return new ProviderInfo(ProviderName, IsAvailable: true,
            StatusMessage: $"Authenticated ({_region}) - {KnownModels.Length} model(s)",
            Models: KnownModels.ToList());
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0070
        builder.AddBedrockChatCompletionService(
            modelId: model.Id);
#pragma warning restore SKEXP0070

        return builder.Build();
    }
}
