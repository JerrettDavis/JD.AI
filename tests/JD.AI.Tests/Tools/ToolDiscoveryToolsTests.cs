using FluentAssertions;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Tools;

#pragma warning disable CA1034 // Do not nest type
#pragma warning disable CA1859 // Use concrete type
public sealed class ToolDiscoveryToolsTests
{
    private static Kernel BuildTestKernel(params string[] pluginNames)
    {
        var kernel = Kernel.CreateBuilder().Build();
        foreach (var name in pluginNames)
        {
            kernel.Plugins.AddFromType<StubPlugin>(name);
        }

        return kernel;
    }

    private static IReadOnlyList<KernelPlugin> SnapshotPlugins(Kernel kernel) =>
        kernel.Plugins.ToList().AsReadOnly();

    // ── discover_tools ──────────────────────────────────────

    [Fact]
    public void DiscoverTools_ShowsUnloadedPlugins()
    {
        var kernel = BuildTestKernel("file", "search");
        var allPlugins = SnapshotPlugins(BuildTestKernel("file", "search", "git", "web", "docker"));
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.DiscoverTools();

        result.Should().Contain("git");
        result.Should().Contain("web");
        result.Should().Contain("docker");
        result.Should().NotContain("### `file`");
        result.Should().NotContain("### `search`");
    }

    [Fact]
    public void DiscoverTools_FilterByCategory()
    {
        var kernel = BuildTestKernel("file");
        var allPlugins = SnapshotPlugins(BuildTestKernel("file", "git", "web", "search"));
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.DiscoverTools(category: "git");

        result.Should().Contain("git");
        result.Should().NotContain("### `web`");
        result.Should().NotContain("### `search`");
    }

    [Fact]
    public void DiscoverTools_FilterByKeyword()
    {
        var kernel = BuildTestKernel("file");
        var allPlugins = SnapshotPlugins(BuildTestKernel("file", "git", "github", "web"));
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.DiscoverTools(keyword: "git");

        result.Should().Contain("git");
        result.Should().Contain("github");
        result.Should().NotContain("### `web`");
    }

    [Fact]
    public void DiscoverTools_Pagination()
    {
        var kernel = BuildTestKernel("file");
        var allNames = Enumerable.Range(1, 25).Select(i => $"plugin{i:D2}").ToArray();
        var allPlugins = SnapshotPlugins(BuildTestKernel(["file", .. allNames]));
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var page1 = tools.DiscoverTools(page: 1);
        var page2 = tools.DiscoverTools(page: 2);
        var page3 = tools.DiscoverTools(page: 3);

        page1.Should().Contain("page 1/3");
        page2.Should().Contain("page 2/3");
        page3.Should().Contain("page 3/3");
    }

    [Fact]
    public void DiscoverTools_NoUnloaded_ShowsEmptyMessage()
    {
        var kernel = BuildTestKernel("file", "git");
        var allPlugins = SnapshotPlugins(kernel);
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.DiscoverTools();

        result.Should().Contain("No matching tools found");
    }

    // ── activate_tool ───────────────────────────────────────

    [Fact]
    public void ActivateTool_AddsPluginToKernel()
    {
        var kernel = BuildTestKernel("file");
        var fullKernel = BuildTestKernel("file", "git", "web");
        var allPlugins = SnapshotPlugins(fullKernel);
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        kernel.Plugins.Should().HaveCount(1);

        var result = tools.ActivateTool("git");

        result.Should().Contain("Activated plugin 'git'");
        kernel.Plugins.Should().Contain(p => p.Name == "git");
    }

    [Fact]
    public void ActivateTool_AlreadyLoaded_ReturnsMessage()
    {
        var kernel = BuildTestKernel("file", "git");
        var allPlugins = SnapshotPlugins(kernel);
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.ActivateTool("git");

        result.Should().Contain("already loaded");
    }

    [Fact]
    public void ActivateTool_NotFound_ReturnsError()
    {
        var kernel = BuildTestKernel("file");
        var allPlugins = SnapshotPlugins(kernel);
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.ActivateTool("nonexistent");

        result.Should().Contain("not found");
    }

    [Fact]
    public void ActivateTool_CaseInsensitive()
    {
        var kernel = BuildTestKernel("file");
        var allPlugins = SnapshotPlugins(BuildTestKernel("file", "Git"));
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.ActivateTool("GIT");

        result.Should().Contain("Activated");
    }

    // ── list_tool_categories ────────────────────────────────

    [Fact]
    public void ListCategories_ShowsAllCategories()
    {
        var kernel = BuildTestKernel("file", "git");
        var allPlugins = SnapshotPlugins(BuildTestKernel("file", "git", "web", "search"));
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.ListCategories();

        result.Should().Contain("Filesystem");
        result.Should().Contain("Git");
        result.Should().Contain("Web");
        result.Should().Contain("Search");
    }

    // ── list_toolbelts ──────────────────────────────────────

    [Fact]
    public void ListToolbelts_ShowsBuiltInLoadouts()
    {
        var kernel = BuildTestKernel("file");
        var allPlugins = SnapshotPlugins(kernel);
        var registry = new ToolLoadoutRegistry();
        var tools = new ToolDiscoveryTools(kernel, registry, allPlugins);

        var result = tools.ListToolbelts();

        result.Should().Contain("minimal");
        result.Should().Contain("developer");
        result.Should().Contain("devops");
        result.Should().Contain("research");
        result.Should().Contain("full");
    }

    // ── Stub plugin ─────────────────────────────────────────

    public sealed class StubPlugin
    {
        [Microsoft.SemanticKernel.KernelFunction("stub_action")]
        [System.ComponentModel.Description("A stub action for testing.")]
        public static string StubAction() => "ok";
    }
}
