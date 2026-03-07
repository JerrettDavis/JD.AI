using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Tools;

#pragma warning disable CA1034 // Do not nest type
public sealed class LoadoutAutoSelectionTests
{
    [Theory]
    [InlineData(256_000, WellKnownLoadouts.Full)]
    [InlineData(128_000, WellKnownLoadouts.Full)]
    [InlineData(100_000, WellKnownLoadouts.Developer)]
    [InlineData(64_000, WellKnownLoadouts.Developer)]
    [InlineData(32_000, WellKnownLoadouts.Minimal)]
    [InlineData(28_000, WellKnownLoadouts.Minimal)]
    [InlineData(16_000, WellKnownLoadouts.Minimal)]
    [InlineData(4_000, WellKnownLoadouts.Minimal)]
    public void SelectLoadoutForContext_ReturnsAppropriateLoadout(int contextWindowTokens, string expectedLoadout)
    {
        var result = AgentLoop.SelectLoadoutForContext(contextWindowTokens);
        result.Should().Be(expectedLoadout);
    }

    [Fact]
    public void ToolDiscovery_AlwaysInMinimalLoadout()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = registry.GetLoadout(WellKnownLoadouts.Minimal);
        loadout.Should().NotBeNull();
        loadout!.DefaultPlugins.Should().Contain("toolDiscovery");
    }

    [Fact]
    public void ToolDiscovery_AlwaysInFullLoadout()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = registry.GetLoadout(WellKnownLoadouts.Full);
        loadout.Should().NotBeNull();
        loadout!.DefaultPlugins.Should().Contain("toolDiscovery");
    }

    [Fact]
    public void ToolDiscovery_InheritedByDeveloperLoadout()
    {
        var registry = new ToolLoadoutRegistry();
        // developer extends minimal, which has toolDiscovery as a default plugin
        var kernel = new Kernel();
        kernel.Plugins.AddFromType<StubPluginForLoadout>("toolDiscovery");
        kernel.Plugins.AddFromType<StubPluginForLoadout>("file");
        kernel.Plugins.AddFromType<StubPluginForLoadout>("git");

        var active = registry.ResolveActivePlugins(WellKnownLoadouts.Developer, kernel.Plugins);
        active.Should().Contain("toolDiscovery");
    }

    [Fact]
    public void ToolDiscovery_InCategoryMap()
    {
        ToolLoadoutRegistry.PluginCategoryMap.Should().ContainKey("toolDiscovery");
        ToolLoadoutRegistry.PluginCategoryMap["toolDiscovery"].Should().Be(ToolCategory.Orchestration);
    }
}

public sealed class StubPluginForLoadout
{
    [KernelFunction("stub")]
    [System.ComponentModel.Description("Stub")]
    public static string Stub() => "ok";
}
