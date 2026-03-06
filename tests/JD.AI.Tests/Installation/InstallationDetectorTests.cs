using FluentAssertions;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

public sealed class InstallationDetectorTests
{
    // ── GetCurrentRid ─────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentRid_ReturnsOsArchFormat()
    {
        var rid = InstallationDetector.GetCurrentRid();
        rid.Should().MatchRegex(@"^(win|osx|linux)-(x64|arm64|x86)$");
    }

    [Fact]
    public void GetCurrentRid_StartsWithKnownOs()
    {
        var rid = InstallationDetector.GetCurrentRid();
        var os = rid.Split('-')[0];
        new[] { "win", "osx", "linux" }.Should().Contain(os);
    }

    [Fact]
    public void GetCurrentRid_ContainsDash()
    {
        var rid = InstallationDetector.GetCurrentRid();
        rid.Should().Contain("-");
    }

    [Fact]
    public void GetCurrentRid_IsDeterministic()
    {
        var rid1 = InstallationDetector.GetCurrentRid();
        var rid2 = InstallationDetector.GetCurrentRid();
        rid1.Should().Be(rid2);
    }

    // ── GetCurrentVersion ─────────────────────────────────────────────────

    [Fact]
    public void GetCurrentVersion_ReturnsNonEmpty()
    {
        var version = InstallationDetector.GetCurrentVersion();
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetCurrentVersion_DoesNotContainPlusMetadata()
    {
        var version = InstallationDetector.GetCurrentVersion();
        version.Should().NotContain("+");
    }

    [Fact]
    public void GetCurrentVersion_IsDeterministic()
    {
        var v1 = InstallationDetector.GetCurrentVersion();
        var v2 = InstallationDetector.GetCurrentVersion();
        v1.Should().Be(v2);
    }

    [Fact]
    public void GetCurrentVersion_LooksLikeSemVer()
    {
        var version = InstallationDetector.GetCurrentVersion();
        // Should contain at least one dot (e.g. "1.0.0" or "0.0.0")
        version.Should().Contain(".");
    }
}
