using Azure;
using Azure.Security.KeyVault.Secrets;
using JD.AI.Credentials.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Credentials;

public sealed class AzureKeyVaultCredentialStoreTests
{
    private readonly SecretClient _client = Substitute.For<SecretClient>();
    private readonly AzureKeyVaultCredentialStore _sut;

    public AzureKeyVaultCredentialStoreTests()
    {
        _sut = new AzureKeyVaultCredentialStore(_client, NullLogger<AzureKeyVaultCredentialStore>.Instance);
    }

    [Fact]
    public void IsAvailable_ReturnsTrue() => Assert.True(_sut.IsAvailable);

    [Fact]
    public void StoreName_ReturnsAzureKeyVault() =>
        Assert.Equal("Azure Key Vault", _sut.StoreName);

    [Fact]
    public async Task GetAsync_ReturnsValue_WhenSecretExists()
    {
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("my-key"), "secret-value");
        _client.GetSecretAsync("my-key", Arg.Any<string?>(), Arg.Any<CancellationToken>())
               .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        var result = await _sut.GetAsync("my-key");

        Assert.Equal("secret-value", result);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenSecretNotFound()
    {
        _client.GetSecretAsync("missing", Arg.Any<string?>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new RequestFailedException(404, "Not found"));

        var result = await _sut.GetAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_CallsSetSecretAsync()
    {
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("api-key"), "value123");
        _client.SetSecretAsync("api-key", "value123", Arg.Any<CancellationToken>())
               .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        await _sut.SetAsync("api-key", "value123");

        await _client.Received(1).SetSecretAsync("api-key", "value123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_NormalizesSlashesInKey()
    {
        var secret = SecretModelFactory.KeyVaultSecret(new SecretProperties("provider-api-key"), "val");
        _client.GetSecretAsync("provider-api-key", Arg.Any<string?>(), Arg.Any<CancellationToken>())
               .Returns(Response.FromValue(secret, Substitute.For<Response>()));

        var result = await _sut.GetAsync("provider/api/key");

        Assert.Equal("val", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_ThrowsForNullOrWhitespace(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetAsync(key));
    }
}
