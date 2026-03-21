namespace JD.AI.Daemon.Tests.Security;

public sealed class ApiKeyAuthProviderTests
{
    [Fact]
    public async Task AuthenticateAsync_ReturnsRegisteredIdentity_AndRejectsUnknownKey()
    {
        var provider = new ApiKeyAuthProvider();
        provider.RegisterKey("alpha-key", "Alpha", GatewayRole.Admin);

        var authenticated = await provider.AuthenticateAsync("alpha-key");
        var missing = await provider.AuthenticateAsync("missing");

        Assert.NotNull(authenticated);
        Assert.Equal("Alpha", authenticated!.DisplayName);
        Assert.Equal(GatewayRole.Admin, authenticated.Role);
        Assert.False(string.IsNullOrWhiteSpace(authenticated.Id));
        Assert.NotEqual(default, authenticated.AuthenticatedAt);
        Assert.Null(missing);
    }
}
