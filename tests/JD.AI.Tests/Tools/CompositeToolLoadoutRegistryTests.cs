using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class CompositeToolLoadoutRegistryTests
{
    private static ToolLoadoutRegistry CreateBuiltIn()
    {
        return new ToolLoadoutRegistry();
    }

    private static ToolLoadoutRegistry CreateCustomRegistry(params ToolLoadout[] loadouts)
    {
        var registry = new ToolLoadoutRegistry();
        foreach (var loadout in loadouts)
            registry.Register(loadout);
        return registry;
    }

    // ── Constructor ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullRegistries_Throws() =>
        FluentActions.Invoking(() => new CompositeToolLoadoutRegistry(null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void Constructor_EmptyRegistries_Throws() =>
        FluentActions.Invoking(() => new CompositeToolLoadoutRegistry())
            .Should().Throw<ArgumentException>();

    // ── GetLoadout ────────────────────────────────────────────────────────

    [Fact]
    public void GetLoadout_FoundInPrimary()
    {
        var primary = CreateCustomRegistry(new ToolLoadout("custom"));
        var fallback = CreateBuiltIn();
        var composite = new CompositeToolLoadoutRegistry(primary, fallback);

        composite.GetLoadout("custom").Should().NotBeNull();
        composite.GetLoadout("custom")!.Name.Should().Be("custom");
    }

    [Fact]
    public void GetLoadout_FoundInFallback()
    {
        var primary = CreateCustomRegistry();
        var fallback = CreateBuiltIn();
        var composite = new CompositeToolLoadoutRegistry(primary, fallback);

        // Built-in registry has "minimal", "developer", etc.
        composite.GetLoadout("minimal").Should().NotBeNull();
    }

    [Fact]
    public void GetLoadout_PrimaryWins_OnConflict()
    {
        var customMinimal = new ToolLoadout("minimal")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "custom-plugin" },
        };
        var primary = CreateCustomRegistry(customMinimal);
        var fallback = CreateBuiltIn();
        var composite = new CompositeToolLoadoutRegistry(primary, fallback);

        var result = composite.GetLoadout("minimal");
        result.Should().NotBeNull();
        result!.DefaultPlugins.Should().Contain("custom-plugin");
    }

    [Fact]
    public void GetLoadout_NotFound_ReturnsNull()
    {
        var composite = new CompositeToolLoadoutRegistry(CreateBuiltIn());
        composite.GetLoadout("nonexistent").Should().BeNull();
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_UnionOfBothRegistries()
    {
        var primary = CreateCustomRegistry(new ToolLoadout("custom-a"));
        var fallback = CreateBuiltIn();
        var composite = new CompositeToolLoadoutRegistry(primary, fallback);

        var all = composite.GetAll();
        all.Should().Contain(l => l.Name == "custom-a");
        all.Should().Contain(l => l.Name == "minimal");
    }

    [Fact]
    public void GetAll_DeduplicatesByName_PrimaryWins()
    {
        var customMinimal = new ToolLoadout("minimal")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "marker" },
        };
        var primary = CreateCustomRegistry(customMinimal);
        var fallback = CreateBuiltIn();
        var composite = new CompositeToolLoadoutRegistry(primary, fallback);

        var all = composite.GetAll();
        var minimalEntries = all.Where(l => string.Equals(l.Name, "minimal", StringComparison.Ordinal)).ToList();
        minimalEntries.Should().HaveCount(1);
        minimalEntries[0].DefaultPlugins.Should().Contain("marker");
    }

    // ── Register ──────────────────────────────────────────────────────────

    [Fact]
    public void Register_ForwardsToPrimary()
    {
        var primary = CreateCustomRegistry();
        var fallback = CreateBuiltIn();
        var composite = new CompositeToolLoadoutRegistry(primary, fallback);

        composite.Register(new ToolLoadout("registered-via-composite"));

        // Should be findable through the composite
        composite.GetLoadout("registered-via-composite").Should().NotBeNull();

        // And should be in the primary registry
        primary.GetLoadout("registered-via-composite").Should().NotBeNull();
    }

    [Fact]
    public void Register_NullLoadout_Throws()
    {
        var composite = new CompositeToolLoadoutRegistry(CreateBuiltIn());
        FluentActions.Invoking(() => composite.Register(null!))
            .Should().Throw<ArgumentNullException>();
    }
}
