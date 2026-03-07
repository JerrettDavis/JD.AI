// Licensed under the MIT License.

using FluentAssertions;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Infrastructure;

// Test tool classes for assembly scanner tests
[ToolPlugin("test_static", Description = "Static test tools")]
public sealed class StaticTestTools
{
    [KernelFunction("test_read")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [System.ComponentModel.Description("Read test data")]
    public static string ReadData() => "data";

    [KernelFunction("test_write")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [System.ComponentModel.Description("Write test data")]
    public static string WriteData() => "written";
}

[ToolPlugin("test_injected", RequiresInjection = true, Description = "Injected test tools")]
public sealed class InjectedTestTools
{
    [KernelFunction("test_injected_op")]
    [ToolSafetyTier(SafetyTier.AlwaysConfirm)]
    [System.ComponentModel.Description("Injected operation")]
    public string InjectedOp() => "injected";
}

[ToolPlugin("test_ordered", Order = 10)]
public sealed class OrderedTestTools
{
    [KernelFunction("test_ordered_op")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [System.ComponentModel.Description("Ordered operation")]
    public static string OrderedOp() => "ordered";
}

public sealed class ToolAssemblyScannerTests
{
    [Fact]
    public void DiscoverPlugins_FindsAttributedClasses()
    {
        var plugins = ToolAssemblyScanner.DiscoverPlugins(typeof(StaticTestTools).Assembly);

        plugins.Should().Contain(p => p.Attribute.Name == "test_static");
        plugins.Should().Contain(p => p.Attribute.Name == "test_injected");
        plugins.Should().Contain(p => p.Attribute.Name == "test_ordered");
    }

    [Fact]
    public void DiscoverPlugins_RespectsOrder()
    {
        var plugins = ToolAssemblyScanner.DiscoverPlugins(typeof(StaticTestTools).Assembly);

        var staticIdx = plugins.ToList().FindIndex(p => string.Equals(p.Attribute.Name, "test_static", StringComparison.Ordinal));
        var orderedIdx = plugins.ToList().FindIndex(p => string.Equals(p.Attribute.Name, "test_ordered", StringComparison.Ordinal));

        // Order 0 (default) should come before Order 10
        staticIdx.Should().BeLessThan(orderedIdx);
    }

    [Fact]
    public void DiscoverPlugins_IncludesDescription()
    {
        var plugins = ToolAssemblyScanner.DiscoverPlugins(typeof(StaticTestTools).Assembly);
        var staticPlugin = plugins.First(p => string.Equals(p.Attribute.Name, "test_static", StringComparison.Ordinal));

        staticPlugin.Attribute.Description.Should().Be("Static test tools");
    }

    [Fact]
    public void DiscoverPlugins_IdentifiesInjectionRequirement()
    {
        var plugins = ToolAssemblyScanner.DiscoverPlugins(typeof(InjectedTestTools).Assembly);

        plugins.Where(p => p.Attribute.RequiresInjection).Should().Contain(p => p.Attribute.Name == "test_injected");
        plugins.Where(p => !p.Attribute.RequiresInjection).Should().Contain(p => p.Attribute.Name == "test_static");
    }

    [Fact]
    public void BuildSafetyTierMap_ExtractsAllTiers()
    {
        var map = ToolAssemblyScanner.BuildSafetyTierMap(typeof(StaticTestTools).Assembly);

        map.Should().ContainKey("test_read");
        map["test_read"].Should().Be(SafetyTier.AutoApprove);

        map.Should().ContainKey("test_write");
        map["test_write"].Should().Be(SafetyTier.ConfirmOnce);

        map.Should().ContainKey("test_injected_op");
        map["test_injected_op"].Should().Be(SafetyTier.AlwaysConfirm);
    }

    [Fact]
    public void BuildSafetyTierMap_IsCaseInsensitive()
    {
        var map = ToolAssemblyScanner.BuildSafetyTierMap(typeof(StaticTestTools).Assembly);

        map.Should().ContainKey("TEST_READ");
        map.Should().ContainKey("Test_Write");
    }

    [Fact]
    public void GetToolManifest_ReturnsAllTools()
    {
        var manifest = ToolAssemblyScanner.GetToolManifest(typeof(StaticTestTools).Assembly);

        manifest.Should().Contain(x => x.PluginName == "test_static" && x.FunctionName == "test_read");
        manifest.Should().Contain(x => x.PluginName == "test_static" && x.FunctionName == "test_write");
        manifest.Should().Contain(x => x.PluginName == "test_injected" && x.FunctionName == "test_injected_op");
    }

    [Fact]
    public void GetToolManifest_IsOrderedByPluginThenFunction()
    {
        var manifest = ToolAssemblyScanner.GetToolManifest(typeof(StaticTestTools).Assembly);

        var names = manifest.Select(x => x.PluginName).ToList();
        names.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void RegisterStaticPlugins_RegistersNonInjected()
    {
        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();

        var needsInjection = ToolAssemblyScanner.RegisterStaticPlugins(
            kernel, typeof(StaticTestTools).Assembly);

        // Static tools should be registered
        kernel.Plugins.Should().Contain(p => p.Name == "test_static");
        kernel.Plugins.Should().Contain(p => p.Name == "test_ordered");

        // Injected tools should be returned for manual registration
        needsInjection.Should().Contain(p => p.Attribute.Name == "test_injected");
        kernel.Plugins.Should().NotContain(p => p.Name == "test_injected");
    }
}
