using FluentAssertions;
using JD.AI.Workflows.Security;

namespace JD.AI.Tests.Workflows.Security;

public sealed class TrustedPublisherRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _registryPath;

    public TrustedPublisherRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-tpr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _registryPath = Path.Combine(_tempDir, "trusted-publishers.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void Trust_AddsPublisher()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);

        registry.Trust("alice");

        registry.IsTrusted("alice").Should().BeTrue();
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void IsTrusted_UnknownPublisher_ReturnsFalse()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);

        registry.IsTrusted("unknown").Should().BeFalse();
    }

    [Fact]
    public void IsTrusted_CaseInsensitive()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);
        registry.Trust("Alice");

        registry.IsTrusted("alice").Should().BeTrue();
        registry.IsTrusted("ALICE").Should().BeTrue();
    }

    [Fact]
    public void Revoke_RevokesTrust()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);
        registry.Trust("alice");
        registry.Revoke("alice");

        registry.IsTrusted("alice").Should().BeFalse();
    }

    [Fact]
    public void Revoke_PreservesRecord()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);
        registry.Trust("alice");
        registry.Revoke("alice");

        var all = registry.GetAll();
        all.Should().HaveCount(1);
        all[0].Revoked.Should().BeTrue();
        all[0].RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void Trust_Duplicate_DoesNotAddTwice()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);
        registry.Trust("alice");
        registry.Trust("alice");

        registry.Count.Should().Be(1);
    }

    [Fact]
    public void PersistsAcrossInstances()
    {
        var reg1 = new TrustedPublisherRegistry(_registryPath);
        reg1.Trust("alice");
        reg1.Trust("bob");

        var reg2 = new TrustedPublisherRegistry(_registryPath);

        reg2.IsTrusted("alice").Should().BeTrue();
        reg2.IsTrusted("bob").Should().BeTrue();
        reg2.Count.Should().Be(2);
    }

    [Fact]
    public void IsTrusted_NullOrEmpty_ReturnsFalse()
    {
        var registry = new TrustedPublisherRegistry(_registryPath);

        registry.IsTrusted("").Should().BeFalse();
        registry.IsTrusted(null!).Should().BeFalse();
    }
}
