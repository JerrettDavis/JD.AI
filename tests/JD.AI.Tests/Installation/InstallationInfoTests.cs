using FluentAssertions;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

public sealed class InstallationInfoTests
{
    // ── InstallKind enum ──────────────────────────────────────────────────

    [Theory]
    [InlineData(InstallKind.DotnetTool, 0)]
    [InlineData(InstallKind.NativeBinary, 1)]
    [InlineData(InstallKind.Winget, 2)]
    [InlineData(InstallKind.Chocolatey, 3)]
    [InlineData(InstallKind.Scoop, 4)]
    [InlineData(InstallKind.Brew, 5)]
    [InlineData(InstallKind.Apt, 6)]
    [InlineData(InstallKind.Unknown, 7)]
    public void InstallKind_Values(InstallKind kind, int expected) =>
        ((int)kind).Should().Be(expected);

    // ── InstallationInfo record ───────────────────────────────────────────

    [Fact]
    public void Construction_AllProperties()
    {
        var info = new InstallationInfo(
            InstallKind.NativeBinary, "/usr/local/bin/jdai", "1.2.3", "linux-x64");
        info.Kind.Should().Be(InstallKind.NativeBinary);
        info.ExecutablePath.Should().Be("/usr/local/bin/jdai");
        info.CurrentVersion.Should().Be("1.2.3");
        info.RuntimeId.Should().Be("linux-x64");
    }

    [Fact]
    public void RecordEquality()
    {
        var a = new InstallationInfo(InstallKind.DotnetTool, "/bin/jdai", "1.0.0", "win-x64");
        var b = new InstallationInfo(InstallKind.DotnetTool, "/bin/jdai", "1.0.0", "win-x64");
        a.Should().Be(b);
    }

    [Fact]
    public void RecordInequality_DifferentKind()
    {
        var a = new InstallationInfo(InstallKind.DotnetTool, "/bin/jdai", "1.0.0", "win-x64");
        var b = new InstallationInfo(InstallKind.NativeBinary, "/bin/jdai", "1.0.0", "win-x64");
        a.Should().NotBe(b);
    }
}
