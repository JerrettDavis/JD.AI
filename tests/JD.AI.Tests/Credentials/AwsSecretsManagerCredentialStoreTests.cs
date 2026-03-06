using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using JD.AI.Credentials.Aws;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Credentials;

public sealed class AwsSecretsManagerCredentialStoreTests
{
    private readonly IAmazonSecretsManager _client = Substitute.For<IAmazonSecretsManager>();
    private readonly AwsSecretsManagerCredentialStore _sut;

    public AwsSecretsManagerCredentialStoreTests()
    {
        _sut = new AwsSecretsManagerCredentialStore(
            _client,
            NullLogger<AwsSecretsManagerCredentialStore>.Instance,
            prefix: "jdai/");
    }

    [Fact]
    public void IsAvailable_ReturnsTrue() => Assert.True(_sut.IsAvailable);

    [Fact]
    public void StoreName_ReturnsAwsSecretsManager() =>
        Assert.Equal("AWS Secrets Manager", _sut.StoreName);

    [Fact]
    public async Task GetAsync_ReturnsValue_WhenSecretExists()
    {
        _client.GetSecretValueAsync(
                   Arg.Is<GetSecretValueRequest>(r => r.SecretId == "jdai/mykey"),
                   Arg.Any<CancellationToken>())
               .Returns(new GetSecretValueResponse { SecretString = "thevalue" });

        var result = await _sut.GetAsync("mykey");

        Assert.Equal("thevalue", result);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenSecretNotFound()
    {
        _client.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new ResourceNotFoundException("Not found"));

        var result = await _sut.GetAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_CallsPutSecretValueAsync_WhenSecretExists()
    {
        _client.PutSecretValueAsync(
                   Arg.Is<PutSecretValueRequest>(r => r.SecretId == "jdai/api-key" && r.SecretString == "val"),
                   Arg.Any<CancellationToken>())
               .Returns(new PutSecretValueResponse());

        await _sut.SetAsync("api-key", "val");

        await _client.Received(1).PutSecretValueAsync(
            Arg.Is<PutSecretValueRequest>(r => r.SecretId == "jdai/api-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_FallsBackToCreateSecretAsync_WhenSecretNotFound()
    {
        _client.PutSecretValueAsync(Arg.Any<PutSecretValueRequest>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new ResourceNotFoundException("Not found"));
        _client.CreateSecretAsync(Arg.Any<CreateSecretRequest>(), Arg.Any<CancellationToken>())
               .Returns(new CreateSecretResponse());

        await _sut.SetAsync("new-key", "value");

        await _client.Received(1).CreateSecretAsync(
            Arg.Is<CreateSecretRequest>(r => r.Name == "jdai/new-key" && r.SecretString == "value"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_CallsDeleteSecretAsync()
    {
        _client.DeleteSecretAsync(Arg.Any<DeleteSecretRequest>(), Arg.Any<CancellationToken>())
               .Returns(new DeleteSecretResponse());

        await _sut.RemoveAsync("mykey");

        await _client.Received(1).DeleteSecretAsync(
            Arg.Is<DeleteSecretRequest>(r => r.SecretId == "jdai/mykey"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_Succeeds_WhenSecretAlreadyGone()
    {
        _client.DeleteSecretAsync(Arg.Any<DeleteSecretRequest>(), Arg.Any<CancellationToken>())
               .ThrowsAsync(new ResourceNotFoundException("Gone"));

        // Should not throw
        await _sut.RemoveAsync("gone");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_ThrowsForNullOrWhitespace(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GetAsync(key));
    }
}
