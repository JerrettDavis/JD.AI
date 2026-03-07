using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class LoadoutResolutionHelperTests
{
    private static Dictionary<string, ToolLoadout> MakeLoadouts(params ToolLoadout[] loadouts) =>
        loadouts.ToDictionary(l => l.Name, l => l, StringComparer.OrdinalIgnoreCase);

    // ── BuildInheritanceChain ────────────────────────────────────────────

    [Fact]
    public void BuildInheritanceChain_SingleLoadout_ReturnsSingleElement()
    {
        var loadout = new ToolLoadout("base");
        var dict = MakeLoadouts(loadout);

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("base", dict);

        chain.Should().ContainSingle().Which.Name.Should().Be("base");
    }

    [Fact]
    public void BuildInheritanceChain_LinearChain_ReturnsRootFirst()
    {
        var grandparent = new ToolLoadout("gp");
        var parent = new ToolLoadout("p") { ParentLoadoutName = "gp" };
        var child = new ToolLoadout("c") { ParentLoadoutName = "p" };
        var dict = MakeLoadouts(grandparent, parent, child);

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("c", dict);

        chain.Should().HaveCount(3);
        chain[0].Name.Should().Be("gp");
        chain[1].Name.Should().Be("p");
        chain[2].Name.Should().Be("c");
    }

    [Fact]
    public void BuildInheritanceChain_CycleDetection_BreaksCycle()
    {
        var a = new ToolLoadout("a") { ParentLoadoutName = "b" };
        var b = new ToolLoadout("b") { ParentLoadoutName = "a" };
        var dict = MakeLoadouts(a, b);

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("a", dict);

        // Should not infinite loop; cycle guard breaks at revisited node
        chain.Should().HaveCount(2);
    }

    [Fact]
    public void BuildInheritanceChain_MissingParent_StopsAtBreak()
    {
        var child = new ToolLoadout("child") { ParentLoadoutName = "nonexistent" };
        var dict = MakeLoadouts(child);

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("child", dict);

        chain.Should().ContainSingle().Which.Name.Should().Be("child");
    }

    [Fact]
    public void BuildInheritanceChain_UnknownLoadout_ReturnsEmpty()
    {
        var dict = MakeLoadouts(new ToolLoadout("only"));

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("nope", dict);

        chain.Should().BeEmpty();
    }

    [Fact]
    public void BuildInheritanceChain_SelfCycle_ReturnsSingle()
    {
        var self = new ToolLoadout("self") { ParentLoadoutName = "self" };
        var dict = MakeLoadouts(self);

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("self", dict);

        chain.Should().ContainSingle().Which.Name.Should().Be("self");
    }

    [Fact]
    public void BuildInheritanceChain_EmptyDictionary_ReturnsEmpty()
    {
        var dict = new Dictionary<string, ToolLoadout>(StringComparer.OrdinalIgnoreCase);

        var chain = LoadoutResolutionHelper.BuildInheritanceChain("any", dict);

        chain.Should().BeEmpty();
    }
}
