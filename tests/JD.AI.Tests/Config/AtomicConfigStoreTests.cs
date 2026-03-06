using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Config;

public class AtomicConfigStoreTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string _configPath;

    public AtomicConfigStoreTests()
    {
        _configPath = Path.Combine(_fixture.DirectoryPath, "config.json");
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task ReadAsync_ReturnsEmptyConfig_WhenFileDoesNotExist()
    {
        var store = new AtomicConfigStore(_configPath);

        var config = await store.ReadAsync();

        config.Should().NotBeNull();
        config.Defaults.Provider.Should().BeNull();
        config.Defaults.Model.Should().BeNull();
        config.ProjectDefaults.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteAsync_CreatesFileAndDirectories()
    {
        var nested = Path.Combine(_fixture.DirectoryPath, "sub", "dir", "config.json");
        var store = new AtomicConfigStore(nested);

        await store.WriteAsync(cfg => cfg.Defaults.Provider = "openai");

        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public async Task ReadAsync_AfterWrite_ReturnsWrittenData()
    {
        var store = new AtomicConfigStore(_configPath);

        await store.WriteAsync(cfg =>
        {
            cfg.Defaults.Provider = "azure";
            cfg.Defaults.Model = "gpt-4o";
        });

        var config = await store.ReadAsync();

        config.Defaults.Provider.Should().Be("azure");
        config.Defaults.Model.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotCorruptFile()
    {
        var store = new AtomicConfigStore(_configPath);

        var tasks = Enumerable.Range(0, 10).Select(i =>
            store.WriteAsync(cfg =>
                cfg.ProjectDefaults[$"/project/{i}"] = new DefaultsConfig
                {
                    Provider = $"provider-{i}",
                    Model = $"model-{i}",
                }));

        await Task.WhenAll(tasks);

        var config = await store.ReadAsync();
        config.ProjectDefaults.Should().HaveCount(10);

        for (var i = 0; i < 10; i++)
        {
            config.ProjectDefaults[$"/project/{i}"].Provider.Should().Be($"provider-{i}");
            config.ProjectDefaults[$"/project/{i}"].Model.Should().Be($"model-{i}");
        }
    }

    [Fact]
    public async Task WriteAsync_CreatesBackupFile()
    {
        var store = new AtomicConfigStore(_configPath);

        // First write — no backup yet (no prior file)
        await store.WriteAsync(cfg => cfg.Defaults.Provider = "first");
        File.Exists(_configPath + ".bak").Should().BeFalse();

        // Second write — backup should contain previous content
        await store.WriteAsync(cfg => cfg.Defaults.Provider = "second");
        File.Exists(_configPath + ".bak").Should().BeTrue();

        var bakContent = await File.ReadAllTextAsync(_configPath + ".bak");
        bakContent.Should().Contain("first");
    }

    [Fact]
    public async Task ProjectDefaults_AreIsolatedFromGlobalDefaults()
    {
        var store = new AtomicConfigStore(_configPath);

        await store.WriteAsync(cfg =>
        {
            cfg.Defaults.Provider = "global-provider";
            cfg.Defaults.Model = "global-model";
            cfg.ProjectDefaults["/my/project"] = new DefaultsConfig
            {
                Provider = "project-provider",
                Model = "project-model",
            };
        });

        var config = await store.ReadAsync();

        config.Defaults.Provider.Should().Be("global-provider");
        config.Defaults.Model.Should().Be("global-model");
        config.ProjectDefaults["/my/project"].Provider.Should().Be("project-provider");
        config.ProjectDefaults["/my/project"].Model.Should().Be("project-model");
    }

    [Fact]
    public async Task GetDefaultProvider_WithProjectPath_ReturnsProjectOverride()
    {
        var store = new AtomicConfigStore(_configPath);

        await store.SetDefaultProviderAsync("global", projectPath: null);
        await store.SetDefaultProviderAsync("local", projectPath: "/my/project");

        var global = await store.GetDefaultProviderAsync();
        var project = await store.GetDefaultProviderAsync("/my/project");
        var unknown = await store.GetDefaultProviderAsync("/other/project");

        global.Should().Be("global");
        project.Should().Be("local");
        unknown.Should().Be("global"); // falls back to global
    }

    [Fact]
    public async Task GetDefaultModel_WithProjectPath_ReturnsProjectOverride()
    {
        var store = new AtomicConfigStore(_configPath);

        await store.SetDefaultModelAsync("gpt-4o", projectPath: null);
        await store.SetDefaultModelAsync("claude-3", projectPath: "/my/project");

        var global = await store.GetDefaultModelAsync();
        var project = await store.GetDefaultModelAsync("/my/project");
        var unknown = await store.GetDefaultModelAsync("/other/project");

        global.Should().Be("gpt-4o");
        project.Should().Be("claude-3");
        unknown.Should().Be("gpt-4o"); // falls back to global
    }
}
