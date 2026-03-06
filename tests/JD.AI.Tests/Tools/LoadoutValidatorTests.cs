using FluentAssertions;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Tools;

public sealed class LoadoutValidatorTests
{
    private static ToolLoadoutRegistry CreateRegistry(params ToolLoadout[] loadouts)
    {
        var registry = new ToolLoadoutRegistry();
        foreach (var loadout in loadouts)
            registry.Register(loadout);
        return registry;
    }

    [Fact]
    public void Validate_ValidLoadout_ReturnsEmpty()
    {
        var registry = CreateRegistry();
        var loadout = new ToolLoadout("test");

        LoadoutValidator.Validate(loadout, registry).Should().BeEmpty();
    }

    [Fact]
    public void IsValid_ValidLoadout_ReturnsTrue()
    {
        var registry = CreateRegistry();
        var loadout = new ToolLoadout("test");

        LoadoutValidator.IsValid(loadout, registry).Should().BeTrue();
    }

    [Fact]
    public void Validate_ParentNotFound_ReturnsError()
    {
        var registry = CreateRegistry();
        var loadout = new ToolLoadout("child") { ParentLoadoutName = "missing-parent" };

        var errors = LoadoutValidator.Validate(loadout, registry);

        errors.Should().ContainSingle()
            .Which.Should().Contain("missing-parent");
    }

    [Fact]
    public void Validate_ParentExists_NoError()
    {
        var parent = new ToolLoadout("base");
        var registry = CreateRegistry(parent);
        var loadout = new ToolLoadout("child") { ParentLoadoutName = "base" };

        LoadoutValidator.Validate(loadout, registry).Should().BeEmpty();
    }

    [Fact]
    public void Validate_PluginInBothIncludeAndExclude_ReturnsError()
    {
        var registry = CreateRegistry();
        var loadout = new ToolLoadout("test")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "conflicting" },
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "conflicting" },
        };

        var errors = LoadoutValidator.Validate(loadout, registry);

        errors.Should().ContainSingle()
            .Which.Should().Contain("conflicting");
    }

    [Fact]
    public void Validate_CircularInheritance_ReturnsError()
    {
        var a = new ToolLoadout("a") { ParentLoadoutName = "b" };
        var b = new ToolLoadout("b") { ParentLoadoutName = "a" };
        var registry = CreateRegistry(a, b);

        var errors = LoadoutValidator.Validate(a, registry);

        errors.Should().Contain(e => e.Contains("Circular"));
    }

    [Fact]
    public void Validate_MultipleErrors()
    {
        var loadout = new ToolLoadout("child")
        {
            ParentLoadoutName = "nonexistent",
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x" },
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x" },
        };
        var registry = CreateRegistry();

        var errors = LoadoutValidator.Validate(loadout, registry);

        errors.Should().HaveCount(2);
    }

    [Fact]
    public void IsValid_WithErrors_ReturnsFalse()
    {
        var registry = CreateRegistry();
        var loadout = new ToolLoadout("bad")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "p" },
            DisabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "p" },
        };

        LoadoutValidator.IsValid(loadout, registry).Should().BeFalse();
    }

    [Fact]
    public void Validate_NullLoadout_Throws() =>
        FluentActions.Invoking(() => LoadoutValidator.Validate(null!, CreateRegistry()))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void Validate_NullRegistry_Throws() =>
        FluentActions.Invoking(() => LoadoutValidator.Validate(new ToolLoadout("x"), null!))
            .Should().Throw<ArgumentNullException>();
}
