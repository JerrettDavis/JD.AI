
using FluentAssertions;
using JD.AI.Core.Security;
using Xunit;

namespace JD.AI.Tests.Security;

public class ApiKeyAuthProviderTests
{
    private readonly ApiKeyAuthProvider _provider = new();

    [Fact]
    public async Task RegisterKey_ThenAuthenticate_ReturnsIdentity()
    {
        _provider.RegisterKey("key-123", "Alice");

        var identity = await _provider.AuthenticateAsync("key-123");

        identity.Should().NotBeNull();
        identity!.DisplayName.Should().Be("Alice");
        identity.Role.Should().Be(GatewayRole.User);
        identity.Id.Should().NotBeNullOrWhiteSpace();
        identity.AuthenticatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Authenticate_InvalidKey_ReturnsNull()
    {
        _provider.RegisterKey("valid-key", "Bob");

        var identity = await _provider.AuthenticateAsync("wrong-key");

        identity.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_EmptyKey_ReturnsNull()
    {
        var identity = await _provider.AuthenticateAsync(string.Empty);

        identity.Should().BeNull();
    }

    [Theory]
    [InlineData(GatewayRole.Admin)]
    [InlineData(GatewayRole.Operator)]
    [InlineData(GatewayRole.Guest)]
    public async Task RegisterKey_WithRole_SetsCorrectRole(GatewayRole role)
    {
        _provider.RegisterKey($"key-{role}", "User", role);

        var identity = await _provider.AuthenticateAsync($"key-{role}");

        identity.Should().NotBeNull();
        identity!.Role.Should().Be(role);
    }

    [Fact]
    public async Task RegisterKey_SameKeyTwice_OverwritesPrevious()
    {
        _provider.RegisterKey("dup-key", "First", GatewayRole.Guest);
        _provider.RegisterKey("dup-key", "Second", GatewayRole.Admin);

        var identity = await _provider.AuthenticateAsync("dup-key");

        identity.Should().NotBeNull();
        identity!.DisplayName.Should().Be("Second");
        identity.Role.Should().Be(GatewayRole.Admin);
    }

    [Fact]
    public async Task AuthenticateAsync_CancellationToken_IsRespected()
    {
        _provider.RegisterKey("ct-key", "CancelUser");

        var identity = await _provider.AuthenticateAsync("ct-key", CancellationToken.None);

        identity.Should().NotBeNull();
        identity!.DisplayName.Should().Be("CancelUser");
    }
}
