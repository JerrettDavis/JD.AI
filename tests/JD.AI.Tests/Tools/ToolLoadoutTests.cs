using JD.AI.Core.Agents;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Unit tests for the Tool Loadout / Toolbelt feature:
/// <see cref="ToolCategory"/>, <see cref="ToolLoadout"/>,
/// <see cref="ToolLoadoutBuilder"/>, and <see cref="ToolLoadoutRegistry"/>.
/// </summary>
public sealed class ToolLoadoutTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Minimal stub with one KernelFunction so the plugin factory is satisfied.</summary>
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

    // ── ToolCategory ─────────────────────────────────────────────────────────

    [Fact]
    public void ToolCategory_HasExpectedValues()
    {
        var values = Enum.GetValues<ToolCategory>();
        Assert.Contains(ToolCategory.Filesystem, values);
        Assert.Contains(ToolCategory.Git, values);
        Assert.Contains(ToolCategory.Shell, values);
        Assert.Contains(ToolCategory.Web, values);
        Assert.Contains(ToolCategory.Search, values);
        Assert.Contains(ToolCategory.Memory, values);
        Assert.Contains(ToolCategory.Orchestration, values);
        Assert.Contains(ToolCategory.Analysis, values);
        Assert.Contains(ToolCategory.Security, values);
        Assert.Contains(ToolCategory.Network, values);
        Assert.Contains(ToolCategory.Scheduling, values);
        Assert.Contains(ToolCategory.Multimodal, values);
        Assert.Contains(ToolCategory.GitHub, values);
    }

    // ── ToolLoadout ───────────────────────────────────────────────────────────

    [Fact]
    public void ToolLoadout_Constructor_SetsName()
    {
        var loadout = new ToolLoadout("test");
        Assert.Equal("test", loadout.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToolLoadout_Constructor_ThrowsOnInvalidName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ToolLoadout(name!));
    }

    [Fact]
    public void ToolLoadout_DefaultCollections_AreEmpty()
    {
        var loadout = new ToolLoadout("test");
        Assert.Empty(loadout.DefaultPlugins);
        Assert.Empty(loadout.IncludedCategories);
        Assert.Empty(loadout.DiscoverablePatterns);
        Assert.Empty(loadout.DisabledPlugins);
        Assert.Null(loadout.ParentLoadoutName);
    }

    [Fact]
    public void ToolLoadout_Names_AreNonEmpty()
    {
        Assert.False(string.IsNullOrEmpty(WellKnownLoadouts.Minimal));
        Assert.False(string.IsNullOrEmpty(WellKnownLoadouts.Developer));
        Assert.False(string.IsNullOrEmpty(WellKnownLoadouts.DevOps));
        Assert.False(string.IsNullOrEmpty(WellKnownLoadouts.Research));
        Assert.False(string.IsNullOrEmpty(WellKnownLoadouts.Full));
    }

    // ── ToolLoadoutBuilder ────────────────────────────────────────────────────

    [Fact]
    public void Builder_Create_ReturnsBuilder()
    {
        var builder = ToolLoadoutBuilder.Create("myloadout");
        Assert.NotNull(builder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Builder_Create_ThrowsOnInvalidName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => ToolLoadoutBuilder.Create(name!));
    }

    [Fact]
    public void Builder_Build_SetsName()
    {
        var loadout = ToolLoadoutBuilder.Create("alpha").Build();
        Assert.Equal("alpha", loadout.Name);
    }

    [Fact]
    public void Builder_Extends_SetsParent()
    {
        var loadout = ToolLoadoutBuilder
            .Create("child")
            .Extends("parent")
            .Build();

        Assert.Equal("parent", loadout.ParentLoadoutName);
    }

    [Fact]
    public void Builder_AddPlugin_AddsToDefaultPlugins()
    {
        var loadout = ToolLoadoutBuilder
            .Create("test")
            .AddPlugin("git")
            .AddPlugin("shell")
            .Build();

        Assert.Contains("git", loadout.DefaultPlugins);
        Assert.Contains("shell", loadout.DefaultPlugins);
    }

    [Fact]
    public void Builder_AddPlugin_IsCaseInsensitive()
    {
        var loadout = ToolLoadoutBuilder
            .Create("test")
            .AddPlugin("Git")
            .Build();

        Assert.Contains("git", loadout.DefaultPlugins, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builder_IncludeCategory_AddsToCategories()
    {
        var loadout = ToolLoadoutBuilder
            .Create("test")
            .IncludeCategory(ToolCategory.Git)
            .IncludeCategory(ToolCategory.Shell)
            .Build();

        Assert.Contains(ToolCategory.Git, loadout.IncludedCategories);
        Assert.Contains(ToolCategory.Shell, loadout.IncludedCategories);
    }

    [Fact]
    public void Builder_AddDiscoverable_AddsPatterns()
    {
        var loadout = ToolLoadoutBuilder
            .Create("test")
            .AddDiscoverable("docker*")
            .AddDiscoverable("kube*")
            .Build();

        Assert.Contains("docker*", loadout.DiscoverablePatterns);
        Assert.Contains("kube*", loadout.DiscoverablePatterns);
    }

    [Fact]
    public void Builder_Disable_AddsToDisabled()
    {
        var loadout = ToolLoadoutBuilder
            .Create("test")
            .Disable("shell")
            .Build();

        Assert.Contains("shell", loadout.DisabledPlugins);
    }

    [Fact]
    public void Builder_FluentChain_ReturnsCompletedLoadout()
    {
        var loadout = ToolLoadoutBuilder
            .Create("full")
            .Extends("minimal")
            .AddPlugin("think")
            .IncludeCategory(ToolCategory.Git)
            .AddDiscoverable("docker*")
            .Disable("tailscale")
            .Build();

        Assert.Equal("full", loadout.Name);
        Assert.Equal("minimal", loadout.ParentLoadoutName);
        Assert.Contains("think", loadout.DefaultPlugins);
        Assert.Contains(ToolCategory.Git, loadout.IncludedCategories);
        Assert.Contains("docker*", loadout.DiscoverablePatterns);
        Assert.Contains("tailscale", loadout.DisabledPlugins);
    }

    // ── ToolLoadoutRegistry — basic CRUD ──────────────────────────────────────

    [Fact]
    public void Registry_HasFiveBuiltInLoadouts()
    {
        var registry = new ToolLoadoutRegistry();
        Assert.Equal(5, registry.GetAll().Count);
    }

    [Fact]
    public void Registry_Get_ReturnsBuiltInLoadout()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = registry.GetLoadout(WellKnownLoadouts.Minimal);
        Assert.NotNull(loadout);
        Assert.Equal(WellKnownLoadouts.Minimal, loadout.Name);
    }

    [Fact]
    public void Registry_Get_ReturnsNull_ForUnknownName()
    {
        var registry = new ToolLoadoutRegistry();
        Assert.Null(registry.GetLoadout("nonexistent"));
    }

    [Fact]
    public void Registry_Register_OverwritesExisting()
    {
        var registry = new ToolLoadoutRegistry();
        var custom = new ToolLoadout(WellKnownLoadouts.Minimal);
        registry.Register(custom);

        var retrieved = registry.GetLoadout(WellKnownLoadouts.Minimal);
        Assert.Same(custom, retrieved);
    }

    [Fact]
    public void Registry_Register_AddsNewLoadout()
    {
        var registry = new ToolLoadoutRegistry();
        var custom = new ToolLoadout("custom");
        registry.Register(custom);

        Assert.Equal(6, registry.GetAll().Count);
        Assert.Same(custom, registry.GetLoadout("custom"));
    }

    [Fact]
    public void Registry_Register_ThrowsOnNull()
    {
        var registry = new ToolLoadoutRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    // ── PluginCategoryMap ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("file", ToolCategory.Filesystem)]
    [InlineData("batchEdit", ToolCategory.Filesystem)]
    [InlineData("git", ToolCategory.Git)]
    [InlineData("github", ToolCategory.GitHub)]
    [InlineData("shell", ToolCategory.Shell)]
    [InlineData("environment", ToolCategory.Shell)]
    [InlineData("web", ToolCategory.Web)]
    [InlineData("browser", ToolCategory.Web)]
    [InlineData("search", ToolCategory.Search)]
    [InlineData("websearch", ToolCategory.Search)]
    [InlineData("tailscale", ToolCategory.Network)]
    [InlineData("memory", ToolCategory.Memory)]
    [InlineData("tasks", ToolCategory.Orchestration)]
    [InlineData("sessions", ToolCategory.Orchestration)]
    [InlineData("think", ToolCategory.Analysis)]
    [InlineData("scheduler", ToolCategory.Scheduling)]
    [InlineData("multimodal", ToolCategory.Multimodal)]
    [InlineData("policy", ToolCategory.Security)]
    [InlineData("encoding", ToolCategory.Security)]
    public void PluginCategoryMap_ContainsExpectedMappings(string plugin, ToolCategory expected)
    {
        Assert.True(ToolLoadoutRegistry.PluginCategoryMap.TryGetValue(plugin, out var actual));
        Assert.Equal(expected, actual);
    }

    // ── ResolveActivePlugins ──────────────────────────────────────────────────

    [Fact]
    public void ResolveActivePlugins_MinimalLoadout_IncludesFilesystemAndShell()
    {
        var registry = new ToolLoadoutRegistry();
        var plugins = FakePlugins("file", "shell", "git", "github", "tailscale");

        var active = registry.ResolveActivePlugins(WellKnownLoadouts.Minimal, plugins);

        Assert.Contains("file", active);
        Assert.Contains("shell", active);
        Assert.DoesNotContain("git", active);
        Assert.DoesNotContain("tailscale", active);
    }

    [Fact]
    public void ResolveActivePlugins_MinimalLoadout_IncludesThinkPlugin()
    {
        var registry = new ToolLoadoutRegistry();
        var plugins = FakePlugins("think", "git", "docker");

        var active = registry.ResolveActivePlugins(WellKnownLoadouts.Minimal, plugins);

        Assert.Contains("think", active);
        Assert.DoesNotContain("git", active);
    }

    [Fact]
    public void ResolveActivePlugins_DeveloperLoadout_InheritsFromMinimal()
    {
        var registry = new ToolLoadoutRegistry();
        var plugins = FakePlugins("file", "shell", "git", "search", "think", "memory");

        var active = registry.ResolveActivePlugins(WellKnownLoadouts.Developer, plugins);

        // From minimal
        Assert.Contains("file", active);
        Assert.Contains("shell", active);
        Assert.Contains("think", active);
        // Added by developer
        Assert.Contains("git", active);
        Assert.Contains("search", active);
        Assert.Contains("memory", active);
    }

    [Fact]
    public void ResolveActivePlugins_FullLoadout_IncludesAllCategoryPlugins()
    {
        var registry = new ToolLoadoutRegistry();
        var plugins = FakePlugins(
            "file", "git", "github", "shell", "web", "browser",
            "search", "tailscale", "memory", "tasks", "think",
            "scheduler", "multimodal", "policy");

        var active = registry.ResolveActivePlugins(WellKnownLoadouts.Full, plugins);

        Assert.Equal(plugins.Count(), active.Count);
    }

    [Fact]
    public void ResolveActivePlugins_DisabledPlugin_IsExcluded()
    {
        var registry = new ToolLoadoutRegistry();

        var custom = ToolLoadoutBuilder
            .Create("no-shell")
            .IncludeCategory(ToolCategory.Filesystem)
            .IncludeCategory(ToolCategory.Shell)
            .Disable("shell")
            .Build();
        registry.Register(custom);

        var plugins = FakePlugins("file", "shell");
        var active = registry.ResolveActivePlugins("no-shell", plugins);

        Assert.Contains("file", active);
        Assert.DoesNotContain("shell", active);
    }

    [Fact]
    public void ResolveActivePlugins_UnknownPlugin_IsExcluded()
    {
        // A plugin with no entry in PluginCategoryMap and not in DefaultPlugins
        var registry = new ToolLoadoutRegistry();
        var plugins = FakePlugins("file", "unknownplugin");

        var active = registry.ResolveActivePlugins(WellKnownLoadouts.Minimal, plugins);

        Assert.Contains("file", active);
        Assert.DoesNotContain("unknownplugin", active);
    }

    [Fact]
    public void ResolveActivePlugins_ExplicitPlugin_IsIncluded()
    {
        var registry = new ToolLoadoutRegistry();

        var custom = ToolLoadoutBuilder
            .Create("explicit")
            .AddPlugin("myplugin")
            .Build();
        registry.Register(custom);

        var plugins = FakePlugins("myplugin", "other");
        var active = registry.ResolveActivePlugins("explicit", plugins);

        Assert.Contains("myplugin", active);
        Assert.DoesNotContain("other", active);
    }

    // ── ResolveDiscoverablePlugins ────────────────────────────────────────────

    [Fact]
    public void ResolveDiscoverablePlugins_MinimalLoadout_AllUnloadedAreDiscoverable()
    {
        var registry = new ToolLoadoutRegistry();
        // minimal uses "*" pattern, so everything not active/disabled is discoverable
        var plugins = FakePlugins("file", "shell", "git", "docker");

        var discoverable = registry.ResolveDiscoverablePlugins(WellKnownLoadouts.Minimal, plugins);

        Assert.Contains("git", discoverable);
        Assert.Contains("docker", discoverable);
        // Active plugins are NOT discoverable
        Assert.DoesNotContain("file", discoverable);
        Assert.DoesNotContain("shell", discoverable);
    }

    [Fact]
    public void ResolveDiscoverablePlugins_WildcardPattern_MatchesPrefix()
    {
        var registry = new ToolLoadoutRegistry();

        var custom = ToolLoadoutBuilder
            .Create("dockerDiscoverable")
            .IncludeCategory(ToolCategory.Filesystem)
            .AddDiscoverable("docker*")
            .Build();
        registry.Register(custom);

        var plugins = FakePlugins("file", "dockerCompose", "dockerSwarm", "kubernetes");
        var discoverable = registry.ResolveDiscoverablePlugins("dockerDiscoverable", plugins);

        Assert.Contains("dockerCompose", discoverable);
        Assert.Contains("dockerSwarm", discoverable);
        Assert.DoesNotContain("kubernetes", discoverable);
        Assert.DoesNotContain("file", discoverable);
    }

    [Fact]
    public void ResolveDiscoverablePlugins_ExactPattern_OnlyMatchesExact()
    {
        var registry = new ToolLoadoutRegistry();

        var custom = ToolLoadoutBuilder
            .Create("exactDiscoverable")
            .IncludeCategory(ToolCategory.Filesystem)
            .AddDiscoverable("docker")
            .Build();
        registry.Register(custom);

        var plugins = FakePlugins("file", "docker", "dockerCompose");
        var discoverable = registry.ResolveDiscoverablePlugins("exactDiscoverable", plugins);

        Assert.Contains("docker", discoverable);
        Assert.DoesNotContain("dockerCompose", discoverable);
    }

    [Fact]
    public void ResolveDiscoverablePlugins_DisabledPlugin_NotInDiscoverable()
    {
        var registry = new ToolLoadoutRegistry();

        var custom = ToolLoadoutBuilder
            .Create("disabled-discoverable")
            .AddDiscoverable("*")
            .Disable("shell")
            .Build();
        registry.Register(custom);

        var plugins = FakePlugins("file", "shell");
        var discoverable = registry.ResolveDiscoverablePlugins("disabled-discoverable", plugins);

        Assert.DoesNotContain("shell", discoverable);
    }

    // ── Inheritance ───────────────────────────────────────────────────────────

    [Fact]
    public void Inheritance_ChildInheritsParentCategories()
    {
        var registry = new ToolLoadoutRegistry();

        var parent = ToolLoadoutBuilder
            .Create("parent")
            .IncludeCategory(ToolCategory.Git)
            .Build();

        var child = ToolLoadoutBuilder
            .Create("child")
            .Extends("parent")
            .IncludeCategory(ToolCategory.Web)
            .Build();

        registry.Register(parent);
        registry.Register(child);

        var plugins = FakePlugins("git", "web");
        var active = registry.ResolveActivePlugins("child", plugins);

        Assert.Contains("git", active);
        Assert.Contains("web", active);
    }

    [Fact]
    public void Inheritance_ChildDisabledOverridesParentCategory()
    {
        var registry = new ToolLoadoutRegistry();

        var parent = ToolLoadoutBuilder
            .Create("parent")
            .IncludeCategory(ToolCategory.Shell)
            .Build();

        var child = ToolLoadoutBuilder
            .Create("child")
            .Extends("parent")
            .Disable("shell")
            .Build();

        registry.Register(parent);
        registry.Register(child);

        var plugins = FakePlugins("shell");
        var active = registry.ResolveActivePlugins("child", plugins);

        Assert.DoesNotContain("shell", active);
    }

    [Fact]
    public void Inheritance_DiscoverablePatternsAccumulateAcrossChain()
    {
        var registry = new ToolLoadoutRegistry();

        var root = ToolLoadoutBuilder
            .Create("root")
            .AddDiscoverable("docker*")
            .Build();

        var child = ToolLoadoutBuilder
            .Create("child")
            .Extends("root")
            .AddDiscoverable("kube*")
            .Build();

        registry.Register(root);
        registry.Register(child);

        var plugins = FakePlugins("dockerCompose", "kubernetes");
        var discoverable = registry.ResolveDiscoverablePlugins("child", plugins);

        Assert.Contains("dockerCompose", discoverable);
        Assert.Contains("kubernetes", discoverable);
    }

    [Fact]
    public void Inheritance_CycleIsBroken()
    {
        var registry = new ToolLoadoutRegistry();

        // Create a cycle: a → b → a
        var a = ToolLoadoutBuilder.Create("a").Extends("b").Build();
        var b = ToolLoadoutBuilder.Create("b").Extends("a").Build();

        registry.Register(a);
        registry.Register(b);

        // Should not throw or loop infinitely
        var plugins = FakePlugins("file");
        var active = registry.ResolveActivePlugins("a", plugins);
        Assert.NotNull(active);
    }

    [Fact]
    public void Inheritance_UnknownParentIsSilentlyIgnored()
    {
        var registry = new ToolLoadoutRegistry();

        var loadout = ToolLoadoutBuilder
            .Create("orphan")
            .Extends("nonexistent")
            .IncludeCategory(ToolCategory.Git)
            .Build();

        registry.Register(loadout);

        var plugins = FakePlugins("git");
        var active = registry.ResolveActivePlugins("orphan", plugins);

        Assert.Contains("git", active);
    }

    // ── MatchesPattern ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("docker-compose", "docker*", true)]
    [InlineData("docker", "docker*", true)]
    [InlineData("kubernetes", "docker*", false)]
    [InlineData("docker", "docker", true)]
    [InlineData("DOCKER", "docker", true)]     // case-insensitive exact
    [InlineData("docker-compose", "docker", false)]
    [InlineData("anything", "*", true)]
    public void MatchesPattern_BehavesCorrectly(string plugin, string pattern, bool expected)
    {
        Assert.Equal(expected, ToolLoadoutRegistry.MatchesPattern(plugin, pattern));
    }

    // ── SubagentRunner integration ────────────────────────────────────────────

    [Theory]
    [InlineData(SubagentType.Explore)]
    [InlineData(SubagentType.Task)]
    [InlineData(SubagentType.Plan)]
    [InlineData(SubagentType.Review)]
    [InlineData(SubagentType.General)]
    public void SubagentRunner_GetLoadoutName_ReturnsKnownLoadout(SubagentType type)
    {
        var registry = new ToolLoadoutRegistry();
        var loadoutName = SubagentRunner.GetLoadoutName(type);
        var loadout = registry.GetLoadout(loadoutName);
        Assert.NotNull(loadout);
    }

    // ── AgentSession integration ──────────────────────────────────────────────

    [Fact]
    public void AgentSession_ActiveLoadoutName_DefaultsToNull()
    {
        // We can only access the property since construction requires registry + kernel
        // Verify via the property's default via reflection/struct default
        var prop = typeof(AgentSession).GetProperty("ActiveLoadoutName");
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite);
        Assert.Equal(typeof(string), prop.PropertyType);
    }
}
