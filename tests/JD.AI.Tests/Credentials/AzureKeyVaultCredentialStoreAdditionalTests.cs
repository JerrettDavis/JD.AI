using Azure;
using Azure.Security.KeyVault.Secrets;
using JD.AI.Credentials.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Credentials;

/// <summary>Additional coverage for AzureKeyVaultCredentialStore — edge cases, DI extension, and ToSecretName.</summary>
public sealed class AzureKeyVaultCredentialStoreAdditionalTests
{
    private static AzureKeyVaultCredentialStore Build(SecretClient client)
        => new(client, NullLogger<AzureKeyVaultCredentialStore>.Instance);

    [Theory]
    [InlineData("mykey", "mykey")]
    [InlineData("provider/api/key", "provider-api-key")]
    [InlineData("my_key", "my-key")]
    [InlineData("some/other_key", "some-other-key")]
    public async Task GetAsync_ReturnsCorrectSecretName(string key, string expectedName)
    {
        // Validate that ToSecretName is applied correctly by checking the secret name used in the GetAsync call.
        var client = Substitute.For<SecretClient>();
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties(expectedName), "value");
        client.GetSecretAsync(expectedName, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        var sut = Build(client);
        var result = await sut.GetAsync(key);
        Assert.Equal("value", result);
    }

    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureKeyVaultCredentialStore(null!, NullLogger<AzureKeyVaultCredentialStore>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var client = Substitute.For<SecretClient>();
        Assert.Throws<ArgumentNullException>(() =>
            new AzureKeyVaultCredentialStore(client, null!));
    }

    [Fact]
    public async Task GetAsync_RethrowsNon404Exception()
    {
        var client = Substitute.For<SecretClient>();
        client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(500, "server error"));

        var sut = Build(client);
        await Assert.ThrowsAsync<RequestFailedException>(() => sut.GetAsync("key"));
    }

    [Fact]
    public async Task SetAsync_RethrowsExceptions()
    {
        var client = Substitute.For<SecretClient>();
        client.SetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(500, "server error"));

        var sut = Build(client);
        await Assert.ThrowsAsync<RequestFailedException>(() => sut.SetAsync("key", "val"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAsync_ThrowsForNullOrWhitespaceKey(string key)
    {
        var sut = Build(Substitute.For<SecretClient>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetAsync(key, "value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveAsync_ThrowsForNullOrWhitespaceKey(string key)
    {
        var sut = Build(Substitute.For<SecretClient>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.RemoveAsync(key));
    }

    [Fact]
    public async Task RemoveAsync_Succeeds_When404()
    {
        var client = Substitute.For<SecretClient>();
        var op = Substitute.For<DeleteSecretOperation>();
        client.StartDeleteSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var sut = Build(client);
        var ex = await Record.ExceptionAsync(() => sut.RemoveAsync("gone"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task RemoveAsync_RethrowsNon404()
    {
        var client = Substitute.For<SecretClient>();
        client.StartDeleteSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(500, "server error"));

        var sut = Build(client);
        await Assert.ThrowsAsync<RequestFailedException>(() => sut.RemoveAsync("key"));
    }

    [Fact]
    public void AddAzureKeyVaultCredentialStore_ThrowsWhenVaultUriNull()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureKeyVaultCredentialStore(o => { /* VaultUri not set */ }));
    }

    [Fact]
    public void AddAzureKeyVaultCredentialStore_ThrowsForNullConfigure()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddAzureKeyVaultCredentialStore(null!));
    }

    [Fact]
    public void AzureKeyVaultCredentialStoreOptions_DefaultVaultUri_IsNull()
    {
        var opts = new AzureKeyVaultCredentialStoreOptions();
        Assert.Null(opts.VaultUri);
    }
}
