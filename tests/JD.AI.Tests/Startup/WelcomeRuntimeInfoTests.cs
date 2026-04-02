using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;
using JD.AI.Startup;

namespace JD.AI.Tests.Startup;

public sealed class WelcomeRuntimeInfoTests
{
    [Fact]
    public void GetDisplayVersion_UsesInformationalVersionWithoutBuildMetadata()
    {
        var assembly = CreateDynamicAssembly(
            informationalVersion: "1.2.3+abc123",
            assemblyVersion: new Version(9, 9, 9, 9));

        var version = WelcomeRuntimeInfo.GetDisplayVersion(assembly);

        version.Should().Be("1.2.3");
    }

    [Fact]
    public void GetDisplayVersion_FallsBackToAssemblyVersionWhenInformationalVersionMissing()
    {
        var assembly = CreateDynamicAssembly(
            informationalVersion: null,
            assemblyVersion: new Version(4, 5, 6, 7));

        var version = WelcomeRuntimeInfo.GetDisplayVersion(assembly);

        version.Should().Be("4.5.6");
    }

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

    private static AssemblyBuilder CreateDynamicAssembly(string? informationalVersion, Version assemblyVersion)
    {
        var name = new AssemblyName($"WelcomeRuntimeInfoTests_{Guid.NewGuid():N}")
        {
            Version = assemblyVersion,
        };

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        if (informationalVersion is not null)
        {
            var ctor = typeof(AssemblyInformationalVersionAttribute)
                .GetConstructor([typeof(string)])!;
            assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [informationalVersion]));
        }

        return assemblyBuilder;
    }
}
