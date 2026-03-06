using FluentAssertions;
using JD.AI.Core.Installation;

namespace JD.AI.Tests.Installation;

public sealed class InstallerFactoryTests
{
    [Fact]
    public void Create_DotnetTool_ReturnsDotnetToolStrategy()
    {
        var info = new InstallationInfo(
            InstallKind.DotnetTool, "/usr/bin/jdai", "1.0.0", "linux-x64");

        var strategy = InstallerFactory.Create(info);
        strategy.Should().BeOfType<DotnetToolStrategy>();
        strategy.Name.Should().Be("dotnet tool");
    }

    [Theory]
    [InlineData(InstallKind.Winget, "winget")]
    [InlineData(InstallKind.Chocolatey, "chocolatey")]
    [InlineData(InstallKind.Scoop, "scoop")]
    [InlineData(InstallKind.Brew, "brew")]
    [InlineData(InstallKind.Apt, "apt")]
    public void Create_PackageManager_ReturnsPackageManagerStrategy(
        InstallKind kind,
        string expectedName)
    {
        var info = new InstallationInfo(
            kind, "/usr/bin/jdai", "1.0.0", "linux-x64");

        var strategy = InstallerFactory.Create(info);
        strategy.Should().BeOfType<PackageManagerStrategy>();
        strategy.Name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(InstallKind.NativeBinary)]
    [InlineData(InstallKind.Unknown)]
    public void Create_NativeOrUnknown_ReturnsGitHubReleaseStrategy(
        InstallKind kind)
    {
        var info = new InstallationInfo(
            kind, "/usr/local/bin/jdai", "1.0.0", "linux-x64");

        var strategy = InstallerFactory.Create(info);
        strategy.Should().BeOfType<GitHubReleaseStrategy>();
        strategy.Name.Should().Be("GitHub release");
    }
}
