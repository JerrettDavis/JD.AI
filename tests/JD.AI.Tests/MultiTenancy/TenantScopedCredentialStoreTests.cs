using JD.AI.Core.MultiTenancy;
using JD.AI.Core.Providers.Credentials;
using NSubstitute;

namespace JD.AI.Tests.MultiTenancy;

public sealed class TenantScopedCredentialStoreTests
{
    private readonly ICredentialStore _inner = Substitute.For<ICredentialStore>();

    [Fact]
    public async Task GetAsync_PrefixesTenantId()
    {
        var ctx = new TenantContext { TenantId = "tenant-a" };
        var sut = new TenantScopedCredentialStore(_inner, ctx);

        await sut.GetAsync("mykey");

        await _inner.Received(1).GetAsync("tenants/tenant-a/mykey", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_PrefixesTenantId()
    {
        var ctx = new TenantContext { TenantId = "tenant-b" };
        var sut = new TenantScopedCredentialStore(_inner, ctx);

        await sut.SetAsync("api-key", "value");

        await _inner.Received(1).SetAsync("tenants/tenant-b/api-key", "value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_PrefixesTenantId()
    {
        var ctx = new TenantContext { TenantId = "tenant-c" };
        var sut = new TenantScopedCredentialStore(_inner, ctx);

        await sut.RemoveAsync("key");

        await _inner.Received(1).RemoveAsync("tenants/tenant-c/key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListKeysAsync_StripsPrefix()
    {
        var ctx = new TenantContext { TenantId = "acme" };
        var sut = new TenantScopedCredentialStore(_inner, ctx);
        _inner.ListKeysAsync("tenants/acme/", Arg.Any<CancellationToken>())
              .Returns([
                  "tenants/acme/openai-key",
                  "tenants/acme/github-token",
              ]);

        var keys = await sut.ListKeysAsync(string.Empty);

        Assert.Equal(2, keys.Count);
        Assert.Contains("openai-key", keys);
        Assert.Contains("github-token", keys);
    }

    [Fact]
    public async Task GetAsync_ThrowsWhenTenantNotResolved()
    {
        var ctx = new TenantContext(); // TenantId = null
        var sut = new TenantScopedCredentialStore(_inner, ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAsync("key"));
    }

    [Fact]
    public void StoreName_IncludesTenantScopedSuffix()
    {
        _inner.StoreName.Returns("EncryptedFileStore");
        var ctx = new TenantContext { TenantId = "org" };
        var sut = new TenantScopedCredentialStore(_inner, ctx);
        Assert.Contains("tenant-scoped", sut.StoreName);
    }
}
