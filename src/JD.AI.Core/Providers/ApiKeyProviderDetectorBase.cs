using JD.AI.Core.Providers.Credentials;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Providers;

/// <summary>
/// Shared template for API-key based provider detectors.
/// Subclasses supply a static <see cref="KnownModels"/> catalog as a fallback
/// and may override <see cref="DiscoverModelsAsync"/> to query their API for
/// the current model list at detection time.
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

    /// <summary>
    /// Static fallback catalog used when <see cref="DiscoverModelsAsync"/> is not
    /// overridden or when live discovery fails.
    /// </summary>
    protected abstract IReadOnlyList<ProviderModelInfo> KnownModels { get; }

    protected virtual string MissingApiKeyMessage => "No API key configured";

    protected virtual string BuildAuthenticatedStatus(int modelCount) =>
        $"Authenticated - {modelCount} model(s)";

    /// <summary>
    /// Override to query the provider's API for its current model list.
    /// The default implementation returns <see cref="KnownModels"/>.
    /// Implementations should catch transport errors and return <see cref="KnownModels"/>
    /// as the fallback.
    /// </summary>
    protected virtual Task<IReadOnlyList<ProviderModelInfo>> DiscoverModelsAsync(
        string apiKey, CancellationToken ct) =>
        Task.FromResult(KnownModels);

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

        IReadOnlyList<ProviderModelInfo> models;
        try
        {
            models = await DiscoverModelsAsync(_apiKey, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // best-effort discovery — fall back to static catalog
        catch
        {
            models = KnownModels;
        }
#pragma warning restore CA1031

        if (models.Count == 0)
            models = KnownModels;

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
