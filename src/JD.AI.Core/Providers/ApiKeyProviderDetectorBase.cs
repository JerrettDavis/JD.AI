using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Shared template for API-key based provider detectors.
/// </summary>
public abstract class ApiKeyProviderDetectorBase : IProviderDetector
{
    private readonly ProviderConfigurationManager _config;
    private readonly string _providerKey;
    private string? _apiKey;

    protected ApiKeyProviderDetectorBase(
        ProviderConfigurationManager config,
        string providerName,
        string providerKey)
    {
        _config = config;
        _providerKey = providerKey;
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    protected abstract IReadOnlyList<ProviderModelInfo> KnownModels { get; }

    protected virtual string MissingApiKeyMessage => "No API key configured";

    protected virtual string BuildAuthenticatedStatus(int modelCount) =>
        $"Authenticated - {modelCount} model(s)";

    public async Task<ProviderInfo> DetectAsync(CancellationToken ct = default)
    {
        _apiKey = await _config.GetCredentialAsync(_providerKey, "apikey", ct)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new ProviderInfo(
                ProviderName,
                IsAvailable: false,
                StatusMessage: MissingApiKeyMessage,
                Models: []);
        }

        var models = KnownModels.ToList();
        return new ProviderInfo(
            ProviderName,
            IsAvailable: true,
            StatusMessage: BuildAuthenticatedStatus(models.Count),
            Models: models);
    }

    public Kernel BuildKernel(ProviderModelInfo model)
    {
        var builder = Kernel.CreateBuilder();
        ConfigureKernel(builder, model, ApiKeyOrThrow());
        return builder.Build();
    }

    protected abstract void ConfigureKernel(
        IKernelBuilder builder,
        ProviderModelInfo model,
        string apiKey);

    private string ApiKeyOrThrow() =>
        _apiKey ?? throw new InvalidOperationException($"{ProviderName} API key not available.");
}
