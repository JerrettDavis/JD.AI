using FluentAssertions;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

public sealed class PackageManagerStrategyTests
{
    // ── Constructor validation ──────────────────────────────────────────────

    [Theory]
    [InlineData(InstallKind.Winget)]
    [InlineData(InstallKind.Chocolatey)]
    [InlineData(InstallKind.Scoop)]
    [InlineData(InstallKind.Brew)]
    [InlineData(InstallKind.Apt)]
    public void Constructor_ValidKinds_DoNotThrow(InstallKind kind)
    {
        var strategy = new PackageManagerStrategy(kind);
        strategy.Should().NotBeNull();
    }

    [Theory]
    [InlineData(InstallKind.DotnetTool)]
    [InlineData(InstallKind.NativeBinary)]
    [InlineData(InstallKind.Unknown)]
    public void Constructor_InvalidKinds_Throw(InstallKind kind)
    {
        var act = () => new PackageManagerStrategy(kind);
        act.Should().Throw<ArgumentException>();
    }

    // ── Name property ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(InstallKind.Winget, "winget")]
    [InlineData(InstallKind.Chocolatey, "chocolatey")]
    [InlineData(InstallKind.Scoop, "scoop")]
    [InlineData(InstallKind.Brew, "brew")]
    [InlineData(InstallKind.Apt, "apt")]
    public void Name_ReturnsExpected(InstallKind kind, string expected)
    {
        var strategy = new PackageManagerStrategy(kind);
        strategy.Name.Should().Be(expected);
    }
}
