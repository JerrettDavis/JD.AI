using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using JD.AI.Credentials.Aws;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Credentials;

/// <summary>Additional coverage for AwsSecretsManagerCredentialStore — edge cases and DI extensions.</summary>
public sealed class AwsSecretsManagerCredentialStoreAdditionalTests
{
    private static AwsSecretsManagerCredentialStore Build(IAmazonSecretsManager client, string prefix = "")
        => new(client, NullLogger<AwsSecretsManagerCredentialStore>.Instance, prefix);

    [Fact]
    public async Task ListKeysAsync_ReturnsMappedKeys()
    {
        var client = Substitute.For<IAmazonSecretsManager>();
        client.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListSecretsResponse
            {
                SecretList = [new SecretListEntry { Name = "jdai/key1" }, new SecretListEntry { Name = "jdai/key2" }],
                NextToken = null,
            });

        var sut = Build(client, "jdai/");
        var keys = await sut.ListKeysAsync("key");

        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public async Task ListKeysAsync_PaginatesUntilNullNextToken()
    {
        var client = Substitute.For<IAmazonSecretsManager>();
        var call = 0;
        client.ListSecretsAsync(Arg.Any<ListSecretsRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                call++;
                return call == 1
                    ? new ListSecretsResponse { SecretList = [new SecretListEntry { Name = "k1" }], NextToken = "page2" }
                    : new ListSecretsResponse { SecretList = [new SecretListEntry { Name = "k2" }], NextToken = null };
            });

        var sut = Build(client);
        var keys = await sut.ListKeysAsync("");

        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public async Task GetAsync_RethrowsNonResourceNotFoundException()
    {
        var client = Substitute.For<IAmazonSecretsManager>();
        client.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("server error"));

        var sut = Build(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync("key"));
    }

    [Fact]
    public async Task SetAsync_RethrowsNonResourceNotFoundException()
    {
        var client = Substitute.For<IAmazonSecretsManager>();
        client.PutSecretValueAsync(Arg.Any<PutSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("server error"));

        var sut = Build(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SetAsync("key", "val"));
    }

    [Fact]
    public async Task RemoveAsync_RethrowsNonResourceNotFoundException()
    {
        var client = Substitute.For<IAmazonSecretsManager>();
        client.DeleteSecretAsync(Arg.Any<DeleteSecretRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("server error"));

        var sut = Build(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RemoveAsync("key"));
    }

    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AwsSecretsManagerCredentialStore(null!, NullLogger<AwsSecretsManagerCredentialStore>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var client = Substitute.For<IAmazonSecretsManager>();
        Assert.Throws<ArgumentNullException>(() =>
            new AwsSecretsManagerCredentialStore(client, null!));
    }

    [Fact]
    public async Task ToSecretId_WithPrefix_PrependsPrefix()
    {
        // Test prefix concatenation via GetAsync returning expected secretId
        var client = Substitute.For<IAmazonSecretsManager>();
        client.GetSecretValueAsync(
                Arg.Is<GetSecretValueRequest>(r => string.Equals(r.SecretId, "prefix/mykey", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse { SecretString = "found" });

        var sut = new AwsSecretsManagerCredentialStore(
            client,
            NullLogger<AwsSecretsManagerCredentialStore>.Instance,
            prefix: "prefix/");

        // Verify no exception = correct secret ID was used
        var result = await sut.GetAsync("mykey");
        Assert.Equal("found", result);
    }

    [Fact]
    public void AddAwsSecretsManagerCredentialStore_RegistersCorrectly()
    {
        // Uses AmazonSecretsManagerClient with explicit region; no network calls at construction time.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAwsSecretsManagerCredentialStore(o =>
        {
            o.Region = "us-east-1";
            o.Prefix = "test/";
        });

        using var sp = services.BuildServiceProvider();
        var store = sp.GetService<JD.AI.Core.Providers.Credentials.ICredentialStore>();
        Assert.NotNull(store);
        Assert.IsType<AwsSecretsManagerCredentialStore>(store);
    }
}
