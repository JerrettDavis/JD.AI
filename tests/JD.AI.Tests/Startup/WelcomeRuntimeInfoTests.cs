using FluentAssertions;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeRuntimeInfoTests
{
    [Fact]
    public void GetDisplayVersion_ReturnsNonEmpty()
    {
        var version = WelcomeRuntimeInfo.GetDisplayVersion();
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetDisplayVersion_DoesNotContainPlusMetadata()
    {
        // InformationalVersion often contains "+commitHash"; GetDisplayVersion strips it
        var version = WelcomeRuntimeInfo.GetDisplayVersion();
        version.Should().NotContain("+");
    }

    [Fact]
    public void GetDisplayVersion_IsNotUnknown()
    {
        // Test assembly should have at least a Version attribute
        var version = WelcomeRuntimeInfo.GetDisplayVersion();
        version.Should().NotBe("unknown");
    }
}
