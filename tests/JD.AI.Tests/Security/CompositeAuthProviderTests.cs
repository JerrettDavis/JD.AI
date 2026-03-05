using System.Text;
using FluentAssertions;
using JD.AI.Core.Security;

namespace JD.AI.Tests.Security;

public sealed class CompositeAuthProviderTests
{
    [Fact]
    public async Task Authenticate_FirstProviderSucceeds_ReturnsImmediately()
    {
        var apiKeyProvider = new ApiKeyAuthProvider();
        apiKeyProvider.RegisterKey("test-key", "Test", GatewayRole.User);

        var composite = new CompositeAuthProvider(apiKeyProvider);

        var identity = await composite.AuthenticateAsync("test-key");

        identity.Should().NotBeNull();
        identity!.DisplayName.Should().Be("Test");
    }

    [Fact]
    public async Task Authenticate_FallsThrough_ToSecondProvider()
    {
        var apiKeyProvider = new ApiKeyAuthProvider();
        var jwtKey = Encoding.UTF8.GetBytes("a-32-byte-jwt-signing-key-here!!");
        var jwtProvider = new JwtAuthProvider(jwtKey);
        var token = jwtProvider.IssueToken("user-1", "JWT User", GatewayRole.Admin);

        var composite = new CompositeAuthProvider(apiKeyProvider, jwtProvider);

        var identity = await composite.AuthenticateAsync(token);

        identity.Should().NotBeNull();
        identity!.DisplayName.Should().Be("JWT User");
    }

    [Fact]
    public async Task Authenticate_NoProviderSucceeds_ReturnsNull()
    {
        var apiKeyProvider = new ApiKeyAuthProvider();
        var composite = new CompositeAuthProvider(apiKeyProvider);

        var identity = await composite.AuthenticateAsync("invalid-credential");

        identity.Should().BeNull();
    }

    [Fact]
    public void ProviderCount_ReturnsCorrectCount()
    {
        var composite = new CompositeAuthProvider(
            new ApiKeyAuthProvider(),
            new ApiKeyAuthProvider());

        composite.ProviderCount.Should().Be(2);
    }
}
