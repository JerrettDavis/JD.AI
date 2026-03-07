using System.ComponentModel;
using FluentAssertions;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Extended tests for <see cref="LoadoutResolutionHelper"/> covering
/// ResolveActivePlugins and ResolveDiscoverablePlugins.
/// </summary>
public sealed class LoadoutResolutionExtendedTests
{
    private sealed class FakeTool
    {
        [KernelFunction("stub")]
        [Description("stub")]
        public static string Stub() => string.Empty;
    }

    private static KernelPlugin FakePlugin(string name) =>
        KernelPluginFactory.CreateFromObject(new FakeTool(), name);

    private static IEnumerable<KernelPlugin> FakePlugins(params string[] names) =>
        names.Select(FakePlugin);

    private static Dictionary<string, ToolLoadout> MakeLoadouts(params ToolLoadout[] loadouts) =>
        loadouts.ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);

    // ── ResolveActivePlugins ────────────────────────────────────────────

    [Fact]
    public void ResolveActivePlugins_IncludesDefaultPlugins()
    {
        var loadout = new ToolLoadout("test")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA", "PluginB" },
        };
        var dict = MakeLoadouts(loadout);
        var plugins = FakePlugins("PluginA", "PluginB", "PluginC");

        var active = LoadoutResolutionHelper.ResolveActivePlugins("test", plugins, dict);

        active.Should().Contain("PluginA");
        active.Should().Contain("PluginB");
        active.Should().NotContain("PluginC");
    }

    [Fact]
    public void ResolveActivePlugins_ExcludesDisabledPlugins()
    {
        var loadout = new ToolLoadout("test")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA", "PluginB" },
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginB" },
        };
        var dict = MakeLoadouts(loadout);
        var plugins = FakePlugins("PluginA", "PluginB");

        var active = LoadoutResolutionHelper.ResolveActivePlugins("test", plugins, dict);

        active.Should().Contain("PluginA");
        active.Should().NotContain("PluginB");
    }

    [Fact]
    public void ResolveActivePlugins_IncludesByCategory()
    {
        // "git" is mapped to ToolCategory.Git in PluginCategoryMap
        var loadout = new ToolLoadout("test")
        {
            IncludedCategories = new HashSet<ToolCategory> { ToolCategory.Git },
        };
        var dict = MakeLoadouts(loadout);
        var plugins = FakePlugins("git", "UnmappedPlugin");

        var active = LoadoutResolutionHelper.ResolveActivePlugins("test", plugins, dict);

        active.Should().Contain("git");
        active.Should().NotContain("UnmappedPlugin");
    }

    [Fact]
    public void ResolveActivePlugins_InheritedSettingsMerge()
    {
        var parent = new ToolLoadout("parent")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA" },
        };
        var child = new ToolLoadout("child")
        {
            ParentLoadoutName = "parent",
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginB" },
        };
        var dict = MakeLoadouts(parent, child);
        var plugins = FakePlugins("PluginA", "PluginB", "PluginC");

        var active = LoadoutResolutionHelper.ResolveActivePlugins("child", plugins, dict);

        active.Should().Contain("PluginA"); // from parent
        active.Should().Contain("PluginB"); // from child
        active.Should().NotContain("PluginC");
    }

    [Fact]
    public void ResolveActivePlugins_DisabledInChild_OverridesParentDefault()
    {
        var parent = new ToolLoadout("parent")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA", "PluginB" },
        };
        var child = new ToolLoadout("child")
        {
            ParentLoadoutName = "parent",
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA" },
        };
        var dict = MakeLoadouts(parent, child);
        var plugins = FakePlugins("PluginA", "PluginB");

        var active = LoadoutResolutionHelper.ResolveActivePlugins("child", plugins, dict);

        active.Should().NotContain("PluginA"); // disabled in child
        active.Should().Contain("PluginB");
    }

    [Fact]
    public void ResolveActivePlugins_UnknownLoadout_ReturnsEmpty()
    {
        var dict = MakeLoadouts(new ToolLoadout("only"));
        var plugins = FakePlugins("PluginA");

        var active = LoadoutResolutionHelper.ResolveActivePlugins("nonexistent", plugins, dict);

        active.Should().BeEmpty();
    }

    [Fact]
    public void ResolveActivePlugins_EmptyPluginList_ReturnsEmpty()
    {
        var loadout = new ToolLoadout("test")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA" },
        };
        var dict = MakeLoadouts(loadout);

        var active = LoadoutResolutionHelper.ResolveActivePlugins("test", [], dict);

        active.Should().BeEmpty();
    }

    // ── ResolveDiscoverablePlugins ───────────────────────────────────────

    [Fact]
    public void ResolveDiscoverablePlugins_MatchesPatterns()
    {
        var loadout = new ToolLoadout("test")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PluginA" },
            DiscoverablePatterns = ["Custom*"],
        };
        var dict = MakeLoadouts(loadout);
        var plugins = FakePlugins("PluginA", "CustomWidget", "CustomGadget", "Unrelated");

        var discoverable = LoadoutResolutionHelper.ResolveDiscoverablePlugins("test", plugins, dict);

        discoverable.Should().Contain("CustomWidget");
        discoverable.Should().Contain("CustomGadget");
        discoverable.Should().NotContain("PluginA"); // already active
        discoverable.Should().NotContain("Unrelated"); // no pattern match
    }

    [Fact]
    public void ResolveDiscoverablePlugins_ExcludesDisabledEvenIfPatternMatches()
    {
        var loadout = new ToolLoadout("test")
        {
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CustomBad" },
            DiscoverablePatterns = ["Custom*"],
        };
        var dict = MakeLoadouts(loadout);
        var plugins = FakePlugins("CustomGood", "CustomBad");

        var discoverable = LoadoutResolutionHelper.ResolveDiscoverablePlugins("test", plugins, dict);

        discoverable.Should().Contain("CustomGood");
        discoverable.Should().NotContain("CustomBad");
    }

    [Fact]
    public void ResolveDiscoverablePlugins_InheritedPatterns()
    {
        var parent = new ToolLoadout("parent")
        {
            DiscoverablePatterns = ["Parent*"],
        };
        var child = new ToolLoadout("child")
        {
            ParentLoadoutName = "parent",
            DiscoverablePatterns = ["Child*"],
        };
        var dict = MakeLoadouts(parent, child);
        var plugins = FakePlugins("ParentWidget", "ChildWidget", "Other");

        var discoverable = LoadoutResolutionHelper.ResolveDiscoverablePlugins("child", plugins, dict);

        discoverable.Should().Contain("ParentWidget");
        discoverable.Should().Contain("ChildWidget");
        discoverable.Should().NotContain("Other");
    }

    [Fact]
    public void ResolveDiscoverablePlugins_NoPatterns_ReturnsEmpty()
    {
        var loadout = new ToolLoadout("test");
        var dict = MakeLoadouts(loadout);
        var plugins = FakePlugins("PluginA", "PluginB");

        var discoverable = LoadoutResolutionHelper.ResolveDiscoverablePlugins("test", plugins, dict);

        discoverable.Should().BeEmpty();
    }
}
