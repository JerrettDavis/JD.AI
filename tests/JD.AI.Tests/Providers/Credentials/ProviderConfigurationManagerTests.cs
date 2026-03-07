using FluentAssertions;
using JD.AI.Core.Providers.Credentials;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Providers.Credentials;

public sealed class ProviderConfigurationManagerTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly EncryptedFileStore _store;
    private readonly ProviderConfigurationManager _manager;

    public ProviderConfigurationManagerTests()
    {
        _store = new EncryptedFileStore(_fixture.DirectoryPath);
        _manager = new ProviderConfigurationManager(_store);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task GetCredentialAsync_FromStore_ReturnsStoreValue()
    {
        await _store.SetAsync("jdai:provider:openai:apikey", "sk-stored-key");

        var result = await _manager.GetCredentialAsync("openai", "apikey");

        result.Should().Be("sk-stored-key");
    }

    [Fact]
    public async Task GetCredentialAsync_FallsBackToEnvVar()
    {
        var envVarName = $"OPENAI_API_KEY";
        var previous = Environment.GetEnvironmentVariable(envVarName);
        try
        {
            Environment.SetEnvironmentVariable(envVarName, "sk-env-key");

            var result = await _manager.GetCredentialAsync("openai", "apikey");

            result.Should().Be("sk-env-key");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, previous);
        }
    }

    [Fact]
    public async Task GetCredentialAsync_NothingConfigured_ReturnsNull()
    {
        // Ensure env var is not set for a provider with no store entry
        var previous = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("MISTRAL_API_KEY", null);

            var result = await _manager.GetCredentialAsync("mistral", "apikey");

            result.Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MISTRAL_API_KEY", previous);
        }
    }

    [Fact]
    public async Task SetCredentialAsync_StoresInStore()
    {
        await _manager.SetCredentialAsync("anthropic", "apikey", "sk-ant-key");

        var stored = await _store.GetAsync("jdai:provider:anthropic:apikey");
        stored.Should().Be("sk-ant-key");
    }

    [Fact]
    public async Task RemoveProviderAsync_RemovesAllKeys()
    {
        await _manager.SetCredentialAsync("azure-openai", "apikey", "key1");
        await _manager.SetCredentialAsync("azure-openai", "endpoint", "https://test.openai.azure.com");

        await _manager.RemoveProviderAsync("azure-openai");

        var apiKey = await _manager.GetCredentialAsync("azure-openai", "apikey");
        var endpoint = await _manager.GetCredentialAsync("azure-openai", "endpoint");
        apiKey.Should().BeNull();
        endpoint.Should().BeNull();
    }

    [Fact]
    public async Task ListConfiguredProvidersAsync_ReturnsDistinctNames()
    {
        await _manager.SetCredentialAsync("openai", "apikey", "k1");
        await _manager.SetCredentialAsync("anthropic", "apikey", "k2");
        await _manager.SetCredentialAsync("anthropic", "orgid", "org1");

        var providers = await _manager.ListConfiguredProvidersAsync();

        providers.Should().HaveCount(2);
        providers.Should().Contain(p => p.Equals("openai", StringComparison.OrdinalIgnoreCase));
        providers.Should().Contain(p => p.Equals("anthropic", StringComparison.OrdinalIgnoreCase));
    }
}
