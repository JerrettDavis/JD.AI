using JD.AI.Connectors.Jira;

namespace JD.AI.Tests.Connectors;

public sealed class JiraApiKeyAuthProviderTests
{
    [Fact]
    public async Task GetAuthorizationHeaderAsync_WithValidCredentials_ReturnsBasicHeader()
    {
        var options = new JiraConnectorOptions
        {
            Email = "user@example.com",
            ApiToken = "my-api-token",
        };
        var provider = new JiraApiKeyAuthProvider(options);

        var header = await provider.GetAuthorizationHeaderAsync();

        Assert.NotNull(header);
        Assert.StartsWith("Basic ", header);
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_WithMissingEmail_ReturnsNull()
    {
        var provider = new JiraApiKeyAuthProvider(new JiraConnectorOptions { ApiToken = "tok" });

        Assert.Null(await provider.GetAuthorizationHeaderAsync());
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_WithMissingToken_ReturnsNull()
    {
        var provider = new JiraApiKeyAuthProvider(new JiraConnectorOptions { Email = "a@b.com" });

        Assert.Null(await provider.GetAuthorizationHeaderAsync());
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithBothCredentials_ReturnsTrue()
    {
        var options = new JiraConnectorOptions { Email = "a@b.com", ApiToken = "token" };
        var provider = new JiraApiKeyAuthProvider(options);

        Assert.True(await provider.IsAuthenticatedAsync());
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithMissingCredentials_ReturnsFalse()
    {
        var provider = new JiraApiKeyAuthProvider(new JiraConnectorOptions());

        Assert.False(await provider.IsAuthenticatedAsync());
    }

    [Fact]
    public void Scheme_IsBasic()
    {
        var provider = new JiraApiKeyAuthProvider(new JiraConnectorOptions());

        Assert.Equal("Basic", provider.Scheme);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new JiraApiKeyAuthProvider(null!));
    }
}
