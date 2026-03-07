using FluentAssertions;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

public sealed class InstallationDetectorExtendedTests
{
    // ── GetCurrentRid architecture coverage ─────────────────────────────

    [Fact]
    public void GetCurrentRid_ReturnsValidRid()
    {
        var rid = InstallationDetector.GetCurrentRid();
        var parts = rid.Split('-');

        parts.Should().HaveCount(2, "RID should be 'os-arch'");
        parts[0].Should().BeOneOf("win", "osx", "linux");
        parts[1].Should().BeOneOf("x64", "arm64", "x86");
    }

    [Fact]
    public void GetCurrentRid_ConsistentAcrossMultipleCalls()
    {
        var results = Enumerable.Range(0, 10)
            .Select(_ => InstallationDetector.GetCurrentRid())
            .Distinct(StringComparer.Ordinal);

        results.Should().ContainSingle(because: "RID should be deterministic");
    }

    // ── GetCurrentVersion edge cases ────────────────────────────────────

    [Fact]
    public void GetCurrentVersion_ReturnsSemanticVersionFormat()
    {
        var version = InstallationDetector.GetCurrentVersion();
        var parts = version.Split('.');

        parts.Should().HaveCountGreaterThanOrEqualTo(2, "version should have at least major.minor");
        foreach (var part in parts.Take(3))
        {
            int.TryParse(part.Split('-')[0], System.Globalization.CultureInfo.InvariantCulture, out _)
                .Should().BeTrue($"version segment '{part}' should start with a number");
        }
    }

    [Fact]
    public void GetCurrentVersion_DoesNotContainGitHash()
    {
        var version = InstallationDetector.GetCurrentVersion();
        // Git metadata is after '+', which should be stripped
        version.Should().NotContain("+");
    }

    [Fact]
    public void GetCurrentVersion_IsNotFallback()
    {
        var version = InstallationDetector.GetCurrentVersion();
        // Shouldn't fall back to 0.0.0 in a real project
        version.Should().NotBe("0.0.0");
    }

    // ── InstallKind enum coverage ───────────────────────────────────────

    [Theory]
    [InlineData(InstallKind.DotnetTool)]
    [InlineData(InstallKind.NativeBinary)]
    [InlineData(InstallKind.Winget)]
    [InlineData(InstallKind.Chocolatey)]
    [InlineData(InstallKind.Scoop)]
    [InlineData(InstallKind.Brew)]
    [InlineData(InstallKind.Apt)]
    [InlineData(InstallKind.Unknown)]
    public void InstallKind_AllValues_AreDefined(InstallKind kind)
    {
        Enum.IsDefined(kind).Should().BeTrue();
    }

    [Fact]
    public void InstallKind_HasExpectedCount()
    {
        Enum.GetValues<InstallKind>().Should().HaveCount(8);
    }

    // ── InstallationInfo record ─────────────────────────────────────────

    [Fact]
    public void InstallationInfo_RecordEquality()
    {
        var a = new InstallationInfo(InstallKind.DotnetTool, "/usr/bin/jdai", "1.2.3", "linux-x64");
        var b = new InstallationInfo(InstallKind.DotnetTool, "/usr/bin/jdai", "1.2.3", "linux-x64");
        a.Should().Be(b);
    }

    [Fact]
    public void InstallationInfo_RecordInequality()
    {
        var a = new InstallationInfo(InstallKind.DotnetTool, "/usr/bin/jdai", "1.2.3", "linux-x64");
        var b = new InstallationInfo(InstallKind.Brew, "/usr/bin/jdai", "1.2.3", "linux-x64");
        a.Should().NotBe(b);
    }

    [Fact]
    public void InstallationInfo_Properties()
    {
        var info = new InstallationInfo(InstallKind.Scoop, @"C:\scoop\apps\jdai\jdai.exe", "2.0.0", "win-x64");
        info.Kind.Should().Be(InstallKind.Scoop);
        info.ExecutablePath.Should().Be(@"C:\scoop\apps\jdai\jdai.exe");
        info.CurrentVersion.Should().Be("2.0.0");
        info.RuntimeId.Should().Be("win-x64");
    }
}
