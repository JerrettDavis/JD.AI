using System.Text;
using FluentAssertions;
using JD.AI.Core.Security;

namespace JD.AI.Tests.Security;

public sealed class JwtAuthProviderTests
{
    private static readonly byte[] TestKey = Encoding.UTF8.GetBytes("this-is-a-32-byte-signing-key!!!");

    [Fact]
    public async Task IssueAndAuthenticate_ValidToken_ReturnsIdentity()
    {
        var provider = new JwtAuthProvider(TestKey);
        var token = provider.IssueToken("user-123", "Alice", GatewayRole.Admin);

        var identity = await provider.AuthenticateAsync(token);

        identity.Should().NotBeNull();
        identity!.Id.Should().Be("user-123");
        identity.DisplayName.Should().Be("Alice");
        identity.Role.Should().Be(GatewayRole.Admin);
    }

    [Fact]
    public async Task Authenticate_WithBearerPrefix_Works()
    {
        var provider = new JwtAuthProvider(TestKey);
        var token = provider.IssueToken("user-1", "Bob", GatewayRole.User);

        var identity = await provider.AuthenticateAsync($"Bearer {token}");

        identity.Should().NotBeNull();
        identity!.Id.Should().Be("user-1");
    }

    [Fact]
    public async Task Authenticate_ExpiredToken_ReturnsNull()
    {
        var provider = new JwtAuthProvider(TestKey, clockSkew: TimeSpan.Zero);
        var token = provider.IssueToken("user-1", "Alice", GatewayRole.User,
            expiry: TimeSpan.FromMilliseconds(-1000)); // Already expired

        var identity = await provider.AuthenticateAsync(token);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_WrongKey_ReturnsNull()
    {
        var provider1 = new JwtAuthProvider(TestKey);
        var otherKey = Encoding.UTF8.GetBytes("different-32-byte-key-for-test!!");
        var provider2 = new JwtAuthProvider(otherKey);

        var token = provider1.IssueToken("user-1", "Alice", GatewayRole.User);
        var identity = await provider2.AuthenticateAsync(token);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_WrongIssuer_ReturnsNull()
    {
        var issuer1 = new JwtAuthProvider(TestKey, issuer: "app-a");
        var issuer2 = new JwtAuthProvider(TestKey, issuer: "app-b");

        var token = issuer1.IssueToken("user-1", "Alice", GatewayRole.User);
        var identity = await issuer2.AuthenticateAsync(token);

        identity.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_MalformedToken_ReturnsNull()
    {
        var provider = new JwtAuthProvider(TestKey);

        var identity = await provider.AuthenticateAsync("not.a.valid.token");

        identity.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_EmptyString_ReturnsNull()
    {
        var provider = new JwtAuthProvider(TestKey);

        var identity = await provider.AuthenticateAsync("");

        identity.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_TamperedPayload_ReturnsNull()
    {
        var provider = new JwtAuthProvider(TestKey);
        var token = provider.IssueToken("user-1", "Alice", GatewayRole.User);

        // Tamper with the payload (middle part)
        var parts = token.Split('.');
        var tampered = $"{parts[0]}.dGFtcGVyZWQ.{parts[2]}";

        var identity = await provider.AuthenticateAsync(tampered);

        identity.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShortKey_ThrowsArgumentException()
    {
        var act = () => new JwtAuthProvider("short"u8.ToArray());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Authenticate_IncludesClaims()
    {
        var provider = new JwtAuthProvider(TestKey);
        var token = provider.IssueToken("user-1", "Alice", GatewayRole.Admin);

        var identity = await provider.AuthenticateAsync(token);

        identity!.Claims.Should().ContainKey("sub").WhoseValue.Should().Be("user-1");
        identity.Claims.Should().ContainKey("role").WhoseValue.Should().Be("Admin");
    }
}
