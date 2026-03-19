using JD.AI.Core.Providers;
using JD.AI.Core.Providers.Credentials;

namespace JD.AI.IntegrationTests;

internal static class ProviderIntegrationTestHelpers
{
    public static ProviderModelInfo SelectPreferredModel(
        IReadOnlyList<ProviderModelInfo> models,
        IReadOnlyList<string>? preferredModelIds = null)
    {
        if (preferredModelIds is not null)
        {
            foreach (var preferred in preferredModelIds)
            {
                var match = models.FirstOrDefault(m =>
                    m.Id.Contains(preferred, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    return match;
            }
        }

        return models[0];
    }

    public static async Task<TempProviderConfiguration> CreateTempProviderConfigurationAsync(
        string scopeName,
        IEnumerable<(string Provider, string Field, string Value)> credentials)
    {
        var storePath = Path.Combine(Path.GetTempPath(), $"jdai-{scopeName}-itest-{Guid.NewGuid():N}");
        var store = new EncryptedFileStore(storePath);

        foreach (var (provider, field, value) in credentials)
            await store.SetAsync($"jdai:provider:{provider}:{field}", value).ConfigureAwait(false);

        return new TempProviderConfiguration(storePath, new ProviderConfigurationManager(store));
    }
}

internal sealed class TempProviderConfiguration : IDisposable
{
    public string StorePath { get; }
    public ProviderConfigurationManager Config { get; }

    public TempProviderConfiguration(string storePath, ProviderConfigurationManager config)
    {
        StorePath = storePath;
        Config = config;
    }

    public void Dispose()
    {
        if (Directory.Exists(StorePath))
            Directory.Delete(StorePath, recursive: true);
    }
}
