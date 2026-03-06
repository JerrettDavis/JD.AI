using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Core.Providers.Metadata;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Startup;

internal static class StartupTestProviderFactory
{
    public static ProviderInfo AvailableProvider(string providerName, params ProviderModelInfo[] models)
    {
        return new ProviderInfo(
            providerName,
            IsAvailable: true,
            StatusMessage: "ready",
            Models: models);
    }

    public static (
        ProviderRegistry Registry,
        ProviderConfigurationManager ProviderConfig,
        ModelMetadataProvider MetadataProvider)
        CreateRegistry(params ProviderInfo[] providers)
    {
        var metadataProvider = new ModelMetadataProvider();
        var detectors = providers.Select(p => new FakeDetector(p.Name, p)).ToArray();
        var registry = new ProviderRegistry(detectors, metadataProvider);
        var providerConfig = new ProviderConfigurationManager(new StubCredentialStore());
        return (registry, providerConfig, metadataProvider);
    }

    private sealed class FakeDetector(string providerName, ProviderInfo result) : IProviderDetector
    {
        public string ProviderName => providerName;

        public Task<ProviderInfo> DetectAsync(CancellationToken ct = default) =>
            Task.FromResult(result);

        public Kernel BuildKernel(ProviderModelInfo model) =>
            Kernel.CreateBuilder().Build();
    }

    private sealed class StubCredentialStore : ICredentialStore
    {
        public bool IsAvailable => true;

        public string StoreName => "Stub";

        public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string value, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
