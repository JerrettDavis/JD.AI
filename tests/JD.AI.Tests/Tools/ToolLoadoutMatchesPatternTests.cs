using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Tests for <see cref="ToolLoadoutRegistry.MatchesPattern"/> internal static method.
/// </summary>
public sealed class ToolLoadoutMatchesPatternTests
{
    [Theory]
    [InlineData("anything", "*", true)]            // wildcard-only matches all
    [InlineData("docker-compose", "docker*", true)] // prefix wildcard match
    [InlineData("kube-ctl", "docker*", false)]      // prefix wildcard no match
    [InlineData("MyPlugin", "myplugin", true)]      // case-insensitive exact
    [InlineData("MyPlugin", "OtherPlugin", false)]  // exact no match
    [InlineData("MCP-SERVER", "mcp*", true)]        // case-insensitive prefix
    [InlineData("mcp-server", "mcp-server", true)]  // exact match same case
    [InlineData("", "*", true)]                     // empty string matches wildcard
    [InlineData("", "", true)]                      // empty matches empty exactly
    [InlineData("abc", "abc*", true)]               // exact prefix (nothing after prefix)
    public void MatchesPattern_ReturnsExpected(string pluginName, string pattern, bool expected)
    {
        ToolLoadoutRegistry.MatchesPattern(pluginName, pattern).Should().Be(expected);
    }

    // ── PluginCategoryMap coverage ────────────────────────────────────────

    [Fact]
    public void PluginCategoryMap_ContainsExpectedPlugins()
    {
        var map = ToolLoadoutRegistry.PluginCategoryMap;

        map["file"].Should().Be(ToolCategory.Filesystem);
        map["git"].Should().Be(ToolCategory.Git);
        map["github"].Should().Be(ToolCategory.GitHub);
        map["shell"].Should().Be(ToolCategory.Shell);
        map["web"].Should().Be(ToolCategory.Web);
        map["search"].Should().Be(ToolCategory.Search);
        map["tailscale"].Should().Be(ToolCategory.Network);
        map["memory"].Should().Be(ToolCategory.Memory);
        map["tasks"].Should().Be(ToolCategory.Orchestration);
        map["think"].Should().Be(ToolCategory.Analysis);
        map["scheduler"].Should().Be(ToolCategory.Scheduling);
        map["multimodal"].Should().Be(ToolCategory.Multimodal);
        map["policy"].Should().Be(ToolCategory.Security);
    }

    [Fact]
    public void PluginCategoryMap_IsCaseInsensitive()
    {
        var map = ToolLoadoutRegistry.PluginCategoryMap;

        map["FILE"].Should().Be(ToolCategory.Filesystem);
        map["Git"].Should().Be(ToolCategory.Git);
        map["SHELL"].Should().Be(ToolCategory.Shell);
    }
}
