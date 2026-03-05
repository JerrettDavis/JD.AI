using FluentAssertions;
using JD.AI.Core.Security;

namespace JD.AI.Tests.Security;

public sealed class ApiKeyRotationTests
{
    [Fact]
    public void GenerateKey_CreatesValidKey()
    {
        var rotation = new ApiKeyRotation();
        var key = rotation.GenerateKey("test-app");

        key.Should().StartWith("jdai_");
        key.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public void Validate_GeneratedKey_ReturnsRecord()
    {
        var rotation = new ApiKeyRotation();
        var key = rotation.GenerateKey("test-app", GatewayRole.Admin);

        var record = rotation.Validate(key);

        record.Should().NotBeNull();
        record!.Name.Should().Be("test-app");
        record.Role.Should().Be(GatewayRole.Admin);
    }

    [Fact]
    public void Validate_UnknownKey_ReturnsNull()
    {
        var rotation = new ApiKeyRotation();

        rotation.Validate("unknown-key").Should().BeNull();
    }

    [Fact]
    public void RotateKey_ReturnsNewKey_RevokesOld()
    {
        var rotation = new ApiKeyRotation();
        var oldKey = rotation.GenerateKey("app");

        var newKey = rotation.RotateKey(oldKey);

        newKey.Should().NotBeNull();
        newKey.Should().NotBe(oldKey);
        rotation.Validate(oldKey).Should().BeNull("old key should be revoked");
        rotation.Validate(newKey!).Should().NotBeNull("new key should be valid");
    }

    [Fact]
    public void RotateKey_PreservesNameAndRole()
    {
        var rotation = new ApiKeyRotation();
        var oldKey = rotation.GenerateKey("admin-app", GatewayRole.Admin);

        var newKey = rotation.RotateKey(oldKey)!;
        var record = rotation.Validate(newKey);

        record!.Name.Should().Be("admin-app");
        record.Role.Should().Be(GatewayRole.Admin);
    }

    [Fact]
    public void RotateKey_UnknownKey_ReturnsNull()
    {
        var rotation = new ApiKeyRotation();

        rotation.RotateKey("nonexistent").Should().BeNull();
    }

    [Fact]
    public void RevokeKey_RevokesSuccessfully()
    {
        var rotation = new ApiKeyRotation();
        var key = rotation.GenerateKey("app");

        rotation.RevokeKey(key).Should().BeTrue();
        rotation.Validate(key).Should().BeNull();
    }

    [Fact]
    public void RevokeKey_UnknownKey_ReturnsFalse()
    {
        var rotation = new ApiKeyRotation();

        rotation.RevokeKey("unknown").Should().BeFalse();
    }

    [Fact]
    public void Validate_ExpiredKey_ReturnsNull()
    {
        var rotation = new ApiKeyRotation();
        var key = rotation.GenerateKey("app", expiry: TimeSpan.FromMilliseconds(-1));

        // Key was created with already-expired expiry
        Thread.Sleep(10);
        rotation.Validate(key).Should().BeNull();
    }

    [Fact]
    public void ActiveKeyCount_TracksCorrectly()
    {
        var rotation = new ApiKeyRotation();
        rotation.GenerateKey("a");
        rotation.GenerateKey("b");
        var keyC = rotation.GenerateKey("c");
        rotation.RevokeKey(keyC);

        rotation.ActiveKeyCount.Should().Be(2);
    }

    [Fact]
    public void GetAllKeys_ReturnsAllIncludingRevoked()
    {
        var rotation = new ApiKeyRotation();
        var k1 = rotation.GenerateKey("a");
        rotation.GenerateKey("b");
        rotation.RevokeKey(k1);

        rotation.GetAllKeys().Should().HaveCount(2);
    }
}
