using FluentAssertions;
using JD.AI.Core.Providers.Credentials;
using Xunit;

namespace JD.AI.Tests.Providers.Credentials;

public class EnvironmentCredentialStoreTests
{
    [Fact]
    public void IsAvailable_ReturnsTrue()
    {
        var store = new EnvironmentCredentialStore();
        store.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void StoreName_ReturnsExpected()
    {
        var store = new EnvironmentCredentialStore();
        store.StoreName.Should().Be("Environment Variable Store");
    }

    [Fact]
    public async Task GetAsync_MissingVar_ReturnsNull()
    {
        var store = new EnvironmentCredentialStore();
        var result = await store.GetAsync("jdai:test:nonexistent:key");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ExistingVar_ReturnsValue()
    {
        const string Key = "jdai:test:env:credential";
        var envName = EnvironmentCredentialStore.KeyToEnvVar(Key);

        try
        {
            Environment.SetEnvironmentVariable(envName, "test-secret-value");
            var store = new EnvironmentCredentialStore();
            var result = await store.GetAsync(Key);
            result.Should().Be("test-secret-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public async Task SetAsync_IsNoOp()
    {
        var store = new EnvironmentCredentialStore();
        // Should not throw
        await store.SetAsync("key", "value");
    }

    [Fact]
    public async Task ListKeysAsync_FindsMatchingVars()
    {
        const string Key = "jdai:provider:testprov:apikey";
        var envName = EnvironmentCredentialStore.KeyToEnvVar(Key);

        try
        {
            Environment.SetEnvironmentVariable(envName, "secret");
            var store = new EnvironmentCredentialStore();
            var keys = await store.ListKeysAsync("jdai:provider:testprov");
            keys.Should().Contain(k => k.Contains("testprov", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    [Fact]
    public void KeyToEnvVar_ConvertsCorrectly()
    {
        EnvironmentCredentialStore.KeyToEnvVar("jdai:provider:openai:apikey")
            .Should().Be("JDAI_PROVIDER_OPENAI_APIKEY");
    }
}
