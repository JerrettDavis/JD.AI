using System.ComponentModel;
using JD.AI.Core.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tests.Tools;

/// <summary>
/// Unit tests for YAML loadout serialisation, <see cref="FileToolLoadoutRegistry"/>,
/// <see cref="CompositeToolLoadoutRegistry"/>, and <see cref="LoadoutValidator"/>.
/// </summary>
public sealed class LoadoutYamlTests : IDisposable
{
    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private readonly string _tempDir;

    public LoadoutYamlTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"loadout-yaml-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteTempLoadout(string filename, string yaml)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, yaml);
        return path;
    }

    // ── ToolLoadoutYamlSerializer — round-trip ────────────────────────────────

    [Fact]
    public void Serialize_ThenDeserialize_PreservesName()
    {
        var loadout = ToolLoadoutBuilder.Create("roundtrip").Build();

        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);
        var result = ToolLoadoutYamlSerializer.Deserialize(yaml);

        Assert.Equal("roundtrip", result.Name);
    }

    [Fact]
    public void Serialize_ThenDeserialize_PreservesParent()
    {
        var loadout = ToolLoadoutBuilder
            .Create("child")
            .Extends("parent")
            .Build();

        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);
        var result = ToolLoadoutYamlSerializer.Deserialize(yaml);

        Assert.Equal("parent", result.ParentLoadoutName);
    }

    [Fact]
    public void Serialize_ThenDeserialize_PreservesIncludedCategories()
    {
        var loadout = ToolLoadoutBuilder
            .Create("cat-test")
            .IncludeCategory(ToolCategory.Git)
            .IncludeCategory(ToolCategory.Search)
            .Build();

        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);
        var result = ToolLoadoutYamlSerializer.Deserialize(yaml);

        Assert.Contains(ToolCategory.Git, result.IncludedCategories);
        Assert.Contains(ToolCategory.Search, result.IncludedCategories);
    }

    [Fact]
    public void Serialize_ThenDeserialize_PreservesDefaultPlugins()
    {
        var loadout = ToolLoadoutBuilder
            .Create("plugins-test")
            .AddPlugin("git")
            .AddPlugin("shell")
            .Build();

        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);
        var result = ToolLoadoutYamlSerializer.Deserialize(yaml);

        Assert.Contains("git", result.DefaultPlugins);
        Assert.Contains("shell", result.DefaultPlugins);
    }

    [Fact]
    public void Serialize_ThenDeserialize_PreservesDisabledPlugins()
    {
        var loadout = ToolLoadoutBuilder
            .Create("disabled-test")
            .Disable("tailscale")
            .Build();

        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);
        var result = ToolLoadoutYamlSerializer.Deserialize(yaml);

        Assert.Contains("tailscale", result.DisabledPlugins);
    }

    [Fact]
    public void Serialize_ThenDeserialize_PreservesDiscoverablePatterns()
    {
        var loadout = ToolLoadoutBuilder
            .Create("discoverable-test")
            .AddDiscoverable("docker*")
            .AddDiscoverable("kube*")
            .Build();

        var yaml = ToolLoadoutYamlSerializer.Serialize(loadout);
        var result = ToolLoadoutYamlSerializer.Deserialize(yaml);

        Assert.Contains("docker*", result.DiscoverablePatterns);
        Assert.Contains("kube*", result.DiscoverablePatterns);
    }

    [Fact]
    public void Deserialize_MinimalYaml_LoadsNameOnly()
    {
        const string YamlText = "name: minimal-yaml\n";
        var result = ToolLoadoutYamlSerializer.Deserialize(YamlText);

        Assert.Equal("minimal-yaml", result.Name);
        Assert.Null(result.ParentLoadoutName);
        Assert.Empty(result.IncludedCategories);
        Assert.Empty(result.DefaultPlugins);
        Assert.Empty(result.DisabledPlugins);
    }

    [Fact]
    public void Deserialize_CategoryIsCaseInsensitive()
    {
        const string YamlText = """
            name: case-test
            includeCategories:
              - git
              - SEARCH
            """;

        var result = ToolLoadoutYamlSerializer.Deserialize(YamlText);

        Assert.Contains(ToolCategory.Git, result.IncludedCategories);
        Assert.Contains(ToolCategory.Search, result.IncludedCategories);
    }

    [Fact]
    public void DeserializeFile_ReadsFromDisk()
    {
        const string YamlText = """
            name: from-file
            includeCategories:
              - Filesystem
            """;

        var path = WriteTempLoadout("test.loadout.yaml", YamlText);
        var result = ToolLoadoutYamlSerializer.DeserializeFile(path);

        Assert.Equal("from-file", result.Name);
        Assert.Contains(ToolCategory.Filesystem, result.IncludedCategories);
    }

    // ── FileToolLoadoutRegistry ───────────────────────────────────────────────

    [Fact]
    public void FileRegistry_EmptyDirectory_LoadsZero()
    {
        var registry = new FileToolLoadoutRegistry([_tempDir]);
        Assert.Equal(0, registry.LoadedCount);
    }

    [Fact]
    public void FileRegistry_LoadsYamlFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "custom.loadout.yaml"),
            "name: custom\nincludeCategories:\n  - Git\n");

        var registry = new FileToolLoadoutRegistry([_tempDir]);

        Assert.Equal(1, registry.LoadedCount);
        Assert.NotNull(registry.GetLoadout("custom"));
    }

    [Fact]
    public void FileRegistry_NonexistentPath_IsSkipped()
    {
        var registry = new FileToolLoadoutRegistry(["/nonexistent/path/xyz"]);
        Assert.Equal(0, registry.LoadedCount);
    }

    [Fact]
    public void FileRegistry_InvalidYaml_IsSkipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "bad.loadout.yaml"), "!!!: [invalid yaml:");

        var registry = new FileToolLoadoutRegistry([_tempDir]);
        Assert.Equal(0, registry.LoadedCount);
    }

    [Fact]
    public void FileRegistry_GetAll_ReturnsLoadedLoadouts()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.loadout.yaml"), "name: a\n");
        File.WriteAllText(Path.Combine(_tempDir, "b.loadout.yaml"), "name: b\n");

        var registry = new FileToolLoadoutRegistry([_tempDir]);
        var names = registry.GetAll().Select(l => l.Name).ToList();

        Assert.Contains("a", names);
        Assert.Contains("b", names);
    }

    [Fact]
    public void FileRegistry_ResolveActivePlugins_UsesLoadedLoadout()
    {
        File.WriteAllText(Path.Combine(_tempDir, "git-only.loadout.yaml"),
            "name: git-only\nincludeCategories:\n  - Git\n");

        var registry = new FileToolLoadoutRegistry([_tempDir]);
        var plugins = FakePlugins("git", "shell");
        var active = registry.ResolveActivePlugins("git-only", plugins);

        Assert.Contains("git", active);
        Assert.DoesNotContain("shell", active);
    }

    // ── CompositeToolLoadoutRegistry ──────────────────────────────────────────

    [Fact]
    public void CompositeRegistry_GetLoadout_PrimaryWins()
    {
        var primary = new FileToolLoadoutRegistry([_tempDir]);
        primary.Register(ToolLoadoutBuilder.Create("shared").AddPlugin("primary-plugin").Build());

        var fallback = new ToolLoadoutRegistry();
        fallback.Register(ToolLoadoutBuilder.Create("shared").AddPlugin("fallback-plugin").Build());

        var composite = new CompositeToolLoadoutRegistry(primary, fallback);
        var loadout = composite.GetLoadout("shared");

        Assert.NotNull(loadout);
        Assert.Contains("primary-plugin", loadout.DefaultPlugins);
    }

    [Fact]
    public void CompositeRegistry_GetLoadout_FallsBackWhenNotInPrimary()
    {
        var primary = new FileToolLoadoutRegistry([_tempDir]);
        var fallback = new ToolLoadoutRegistry(); // has built-in "minimal"

        var composite = new CompositeToolLoadoutRegistry(primary, fallback);
        var loadout = composite.GetLoadout(WellKnownLoadouts.Minimal);

        Assert.NotNull(loadout);
    }

    [Fact]
    public void CompositeRegistry_GetAll_UnionDeduplicatedByName()
    {
        var primary = new FileToolLoadoutRegistry([_tempDir]);
        primary.Register(ToolLoadoutBuilder.Create("custom-a").Build());

        var fallback = new ToolLoadoutRegistry(); // 5 built-ins

        var composite = new CompositeToolLoadoutRegistry(primary, fallback);
        var all = composite.GetAll();

        // 5 built-ins + 1 custom
        Assert.Equal(6, all.Count);
        Assert.True(all.Select(l => l.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == all.Count,
            "Names should be unique.");
    }

    [Fact]
    public void CompositeRegistry_GetLoadout_ReturnsNull_WhenNotFound()
    {
        var composite = new CompositeToolLoadoutRegistry(new FileToolLoadoutRegistry([_tempDir]));
        Assert.Null(composite.GetLoadout("does-not-exist"));
    }

    [Fact]
    public void CompositeRegistry_ResolveActivePlugins_CrossRegistryInheritance()
    {
        // YAML loadout that inherits from a built-in ("minimal")
        File.WriteAllText(Path.Combine(_tempDir, "extends-minimal.loadout.yaml"), """
            name: extends-minimal
            parent: minimal
            includeCategories:
              - Git
            """);

        var fileRegistry = new FileToolLoadoutRegistry([_tempDir]);
        var builtInRegistry = new ToolLoadoutRegistry();
        var composite = new CompositeToolLoadoutRegistry(fileRegistry, builtInRegistry);

        // "file" and "shell" come from minimal; "git" from extends-minimal
        var plugins = FakePlugins("file", "shell", "git", "github");
        var active = composite.ResolveActivePlugins("extends-minimal", plugins);

        Assert.Contains("git", active);
        Assert.Contains("file", active);   // inherited from minimal (Filesystem category)
        Assert.Contains("shell", active);  // inherited from minimal (Shell category)
        Assert.DoesNotContain("github", active);
    }

    // ── LoadoutValidator ──────────────────────────────────────────────────────

    [Fact]
    public void Validator_ValidLoadout_ReturnsNoErrors()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = ToolLoadoutBuilder.Create("valid").Build();

        var errors = LoadoutValidator.Validate(loadout, registry);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validator_IsValid_ReturnsTrueForValidLoadout()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = ToolLoadoutBuilder.Create("valid").Build();

        Assert.True(LoadoutValidator.IsValid(loadout, registry));
    }

    [Fact]
    public void Validator_MissingParent_ReturnsError()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = ToolLoadoutBuilder.Create("child").Extends("nonexistent").Build();

        var errors = LoadoutValidator.Validate(loadout, registry);
        Assert.Contains(errors, e => e.Contains("nonexistent"));
    }

    [Fact]
    public void Validator_CircularInheritance_ReturnsError()
    {
        var registry = new ToolLoadoutRegistry();
        var a = ToolLoadoutBuilder.Create("a").Extends("b").Build();
        var b = ToolLoadoutBuilder.Create("b").Extends("a").Build();
        registry.Register(a);
        registry.Register(b);

        var errors = LoadoutValidator.Validate(a, registry);
        Assert.Contains(errors, e => e.Contains("Circular"));
    }

    [Fact]
    public void Validator_IncludeExcludeConflict_ReturnsError()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = ToolLoadoutBuilder
            .Create("conflict")
            .AddPlugin("git")
            .Disable("git")
            .Build();

        var errors = LoadoutValidator.Validate(loadout, registry);
        Assert.Contains(errors, e => e.Contains("git"));
    }

    [Fact]
    public void Validator_ValidParent_ReturnsNoError()
    {
        var registry = new ToolLoadoutRegistry();
        var loadout = ToolLoadoutBuilder.Create("child").Extends(WellKnownLoadouts.Minimal).Build();

        var errors = LoadoutValidator.Validate(loadout, registry);
        Assert.DoesNotContain(errors, e => e.Contains("not found"));
    }
}
