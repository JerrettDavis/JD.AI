using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class ToolLoadoutBuilderTests
{
    [Fact]
    public void Create_SetsName()
    {
        var loadout = ToolLoadoutBuilder.Create("test").Build();
        loadout.Name.Should().Be("test");
    }

    [Fact]
    public void Create_NullName_Throws() =>
        FluentActions.Invoking(() => ToolLoadoutBuilder.Create(null!))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Create_EmptyName_Throws() =>
        FluentActions.Invoking(() => ToolLoadoutBuilder.Create(""))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Extends_SetsParent()
    {
        var loadout = ToolLoadoutBuilder.Create("child")
            .Extends("minimal")
            .Build();
        loadout.ParentLoadoutName.Should().Be("minimal");
    }

    [Fact]
    public void Extends_NullParent_Throws() =>
        FluentActions.Invoking(() => ToolLoadoutBuilder.Create("x").Extends(null!))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void AddPlugin_AddsToDefaultPlugins()
    {
        var loadout = ToolLoadoutBuilder.Create("test")
            .AddPlugin("file")
            .AddPlugin("git")
            .Build();

        loadout.DefaultPlugins.Should().HaveCount(2);
        loadout.DefaultPlugins.Should().Contain("file");
        loadout.DefaultPlugins.Should().Contain("git");
    }

    [Fact]
    public void AddPlugin_CaseInsensitiveDedupe()
    {
        var loadout = ToolLoadoutBuilder.Create("test")
            .AddPlugin("File")
            .AddPlugin("file")
            .Build();

        loadout.DefaultPlugins.Should().HaveCount(1);
    }

    [Fact]
    public void IncludeCategory_AddsToCategories()
    {
        var loadout = ToolLoadoutBuilder.Create("test")
            .IncludeCategory(ToolCategory.Git)
            .IncludeCategory(ToolCategory.Search)
            .Build();

        loadout.IncludedCategories.Should().HaveCount(2);
        loadout.IncludedCategories.Should().Contain(ToolCategory.Git);
        loadout.IncludedCategories.Should().Contain(ToolCategory.Search);
    }

    [Fact]
    public void AddDiscoverable_AddsPattern()
    {
        var loadout = ToolLoadoutBuilder.Create("test")
            .AddDiscoverable("docker*")
            .AddDiscoverable("github")
            .Build();

        loadout.DiscoverablePatterns.Should().HaveCount(2);
        loadout.DiscoverablePatterns.Should().Contain("docker*");
    }

    [Fact]
    public void Disable_AddsToDisabledPlugins()
    {
        var loadout = ToolLoadoutBuilder.Create("test")
            .Disable("dangerous_tool")
            .Build();

        loadout.DisabledPlugins.Should().ContainSingle()
            .Which.Should().Be("dangerous_tool");
    }

    [Fact]
    public void Build_DefaultCollections_AreEmpty()
    {
        var loadout = ToolLoadoutBuilder.Create("test").Build();

        loadout.ParentLoadoutName.Should().BeNull();
        loadout.DefaultPlugins.Should().BeEmpty();
        loadout.IncludedCategories.Should().BeEmpty();
        loadout.DiscoverablePatterns.Should().BeEmpty();
        loadout.DisabledPlugins.Should().BeEmpty();
    }

    [Fact]
    public void Build_FullyConfigured()
    {
        var loadout = ToolLoadoutBuilder.Create("custom")
            .Extends("minimal")
            .AddPlugin("myPlugin")
            .IncludeCategory(ToolCategory.Git)
            .AddDiscoverable("docker*")
            .Disable("ssh")
            .Build();

        loadout.Name.Should().Be("custom");
        loadout.ParentLoadoutName.Should().Be("minimal");
        loadout.DefaultPlugins.Should().Contain("myPlugin");
        loadout.IncludedCategories.Should().Contain(ToolCategory.Git);
        loadout.DiscoverablePatterns.Should().Contain("docker*");
        loadout.DisabledPlugins.Should().Contain("ssh");
    }

    [Fact]
    public void FluentChaining_ReturnsSameBuilder()
    {
        var builder = ToolLoadoutBuilder.Create("test");

        var returned = builder
            .Extends("base")
            .AddPlugin("p1")
            .IncludeCategory(ToolCategory.Shell)
            .AddDiscoverable("*")
            .Disable("d1");

        returned.Should().BeSameAs(builder);
    }
}
