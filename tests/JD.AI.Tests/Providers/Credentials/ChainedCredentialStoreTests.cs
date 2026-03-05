using FluentAssertions;
using JD.AI.Core.Providers.Credentials;
using NSubstitute;
using Xunit;

namespace JD.AI.Tests.Providers.Credentials;

public class ChainedCredentialStoreTests
{
    [Fact]
    public async Task GetAsync_ReturnsFirstNonNullValue()
    {
        var store1 = Substitute.For<ICredentialStore>();
        store1.IsAvailable.Returns(true);
        store1.StoreName.Returns("Store1");
        store1.GetAsync("key", Arg.Any<CancellationToken>()).Returns((string?)null);

        var store2 = Substitute.For<ICredentialStore>();
        store2.IsAvailable.Returns(true);
        store2.StoreName.Returns("Store2");
        store2.GetAsync("key", Arg.Any<CancellationToken>()).Returns("secret");

        var chained = new ChainedCredentialStore(store1, store2);
        var result = await chained.GetAsync("key");

        result.Should().Be("secret");
    }

    [Fact]
    public async Task GetAsync_AllReturnNull_ReturnsNull()
    {
        var store1 = Substitute.For<ICredentialStore>();
        store1.IsAvailable.Returns(true);
        store1.StoreName.Returns("Store1");
        store1.GetAsync("key", Arg.Any<CancellationToken>()).Returns((string?)null);

        var chained = new ChainedCredentialStore(store1);
        var result = await chained.GetAsync("key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WritesToFirstStore()
    {
        var store1 = Substitute.For<ICredentialStore>();
        store1.IsAvailable.Returns(true);
        store1.StoreName.Returns("Store1");

        var store2 = Substitute.For<ICredentialStore>();
        store2.IsAvailable.Returns(true);
        store2.StoreName.Returns("Store2");

        var chained = new ChainedCredentialStore(store1, store2);
        await chained.SetAsync("key", "value");

        await store1.Received(1).SetAsync("key", "value", Arg.Any<CancellationToken>());
        await store2.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListKeysAsync_MergesFromAllStores()
    {
        var store1 = Substitute.For<ICredentialStore>();
        store1.IsAvailable.Returns(true);
        store1.StoreName.Returns("Store1");
        store1.ListKeysAsync("prefix", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "prefix:a" });

        var store2 = Substitute.For<ICredentialStore>();
        store2.IsAvailable.Returns(true);
        store2.StoreName.Returns("Store2");
        store2.ListKeysAsync("prefix", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "prefix:b" });

        var chained = new ChainedCredentialStore(store1, store2);
        var keys = await chained.ListKeysAsync("prefix");

        keys.Should().BeEquivalentTo(["prefix:a", "prefix:b"]);
    }

    [Fact]
    public void SkipsUnavailableStores()
    {
        var available = Substitute.For<ICredentialStore>();
        available.IsAvailable.Returns(true);
        available.StoreName.Returns("Available");

        var unavailable = Substitute.For<ICredentialStore>();
        unavailable.IsAvailable.Returns(false);
        unavailable.StoreName.Returns("Unavailable");

        var chained = new ChainedCredentialStore(unavailable, available);
        chained.StoreName.Should().Contain("Available");
        chained.StoreName.Should().NotContain("Unavailable");
    }
}
