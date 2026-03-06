using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class FileToolLoadoutRegistryTests : IDisposable
{
    private readonly string _tempDir;

    public FileToolLoadoutRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteLoadoutFile(string name, string yaml)
    {
        var filePath = Path.Combine(_tempDir, $"{name}.loadout.yaml");
        File.WriteAllText(filePath, yaml);
    }

    // ── Constructor ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSearchPaths_Throws() =>
        FluentActions.Invoking(() => new FileToolLoadoutRegistry(null!))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void Constructor_EmptySearchPaths_ZeroLoadouts()
    {
        var registry = new FileToolLoadoutRegistry([]);
        registry.LoadedCount.Should().Be(0);
        registry.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NonexistentPath_SkipsGracefully()
    {
        var registry = new FileToolLoadoutRegistry(["/nonexistent/path"]);
        registry.LoadedCount.Should().Be(0);
    }

    // ── Loading from disk ─────────────────────────────────────────────────

    [Fact]
    public void Constructor_LoadsYamlFiles()
    {
        WriteLoadoutFile("test", """
            name: test-loadout
            includeCategories:
              - Git
            """);

        var registry = new FileToolLoadoutRegistry([_tempDir]);

        registry.LoadedCount.Should().Be(1);
        var loadout = registry.GetLoadout("test-loadout");
        loadout.Should().NotBeNull();
        loadout!.IncludedCategories.Should().Contain(ToolCategory.Git);
    }

    [Fact]
    public void Constructor_SkipsInvalidYaml()
    {
        WriteLoadoutFile("good", "name: good-one");
        WriteLoadoutFile("bad", "{{{{invalid yaml");

        var registry = new FileToolLoadoutRegistry([_tempDir]);

        // Bad file should be skipped, good file should load
        registry.GetLoadout("good-one").Should().NotBeNull();
    }

    [Fact]
    public void Constructor_MultipleDirectories()
    {
        var dir2 = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(dir2);
        File.WriteAllText(Path.Combine(dir2, "nested.loadout.yaml"), "name: nested");

        WriteLoadoutFile("top", "name: top-level");

        var registry = new FileToolLoadoutRegistry([_tempDir]);

        registry.GetLoadout("top-level").Should().NotBeNull();
        registry.GetLoadout("nested").Should().NotBeNull();
    }

    // ── Register ──────────────────────────────────────────────────────────

    [Fact]
    public void Register_AddsLoadout()
    {
        var registry = new FileToolLoadoutRegistry([]);
        registry.Register(new ToolLoadout("dynamic"));

        registry.GetLoadout("dynamic").Should().NotBeNull();
    }

    [Fact]
    public void Register_NullLoadout_Throws()
    {
        var registry = new FileToolLoadoutRegistry([]);
        FluentActions.Invoking(() => registry.Register(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_OverwritesExisting()
    {
        var registry = new FileToolLoadoutRegistry([]);
        registry.Register(new ToolLoadout("test"));
        registry.Register(new ToolLoadout("test")
        {
            DefaultPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "new-plugin" },
        });

        registry.GetLoadout("test")!.DefaultPlugins.Should().Contain("new-plugin");
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllLoadouts()
    {
        WriteLoadoutFile("a", "name: alpha");
        WriteLoadoutFile("b", "name: beta");

        var registry = new FileToolLoadoutRegistry([_tempDir]);

        registry.GetAll().Should().HaveCount(2);
    }

    // ── GetLoadout ────────────────────────────────────────────────────────

    [Fact]
    public void GetLoadout_CaseInsensitive()
    {
        WriteLoadoutFile("test", "name: MyLoadout");

        var registry = new FileToolLoadoutRegistry([_tempDir]);

        registry.GetLoadout("myloadout").Should().NotBeNull();
        registry.GetLoadout("MYLOADOUT").Should().NotBeNull();
    }

    [Fact]
    public void GetLoadout_NotFound_ReturnsNull()
    {
        var registry = new FileToolLoadoutRegistry([]);
        registry.GetLoadout("missing").Should().BeNull();
    }
}
