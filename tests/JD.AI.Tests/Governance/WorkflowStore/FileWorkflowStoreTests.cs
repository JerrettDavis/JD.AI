using FluentAssertions;
using JD.AI.Workflows.Store;

namespace JD.AI.Tests.Governance.WorkflowStore;

public class FileWorkflowStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileWorkflowStore _store;

    public FileWorkflowStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-store-{Guid.NewGuid():N}");
        _store = new FileWorkflowStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static SharedWorkflow MakeWorkflow(
        string name = "test-workflow",
        string version = "1.0.0",
        string author = "alice",
        string description = "A test",
        string[]? tags = null) =>
        new()
        {
            Name = name,
            Version = version,
            Author = author,
            Description = description,
            Tags = tags ?? ["ci"],
            DefinitionJson = $"{{\"name\":\"{name}\"}}",
        };

    // ── Publish ───────────────────────────────────────────────

    [Fact]
    public async Task Publish_CreatesJsonFile()
    {
        var workflow = MakeWorkflow("my-workflow", "1.0.0");

        await _store.PublishAsync(workflow);

        var expectedPath = Path.Combine(_tempDir, "my-workflow", "1.0.0.json");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task Publish_FileContainsValidJson()
    {
        var workflow = MakeWorkflow("json-check", "1.0.0");

        await _store.PublishAsync(workflow);

        var path = Path.Combine(_tempDir, "json-check", "1.0.0.json");
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("\"name\":");
        content.Should().Contain("json-check");
    }

    [Fact]
    public async Task Publish_MultipleVersions_CreatesMultipleFiles()
    {
        await _store.PublishAsync(MakeWorkflow("multi", "1.0.0"));
        await _store.PublishAsync(MakeWorkflow("multi", "2.0.0"));

        var dir = Path.Combine(_tempDir, "multi");
        Directory.GetFiles(dir, "*.json").Should().HaveCount(2);
    }

    // ── Catalog ───────────────────────────────────────────────

    [Fact]
    public async Task Catalog_ReturnsAllPublishedWorkflows()
    {
        await _store.PublishAsync(MakeWorkflow("alpha"));
        await _store.PublishAsync(MakeWorkflow("beta"));
        await _store.PublishAsync(MakeWorkflow("gamma"));

        var catalog = await _store.CatalogAsync();
        catalog.Should().HaveCount(3);
        catalog.Select(w => w.Name).Should().BeEquivalentTo(["alpha", "beta", "gamma"]);
    }

    [Fact]
    public async Task Catalog_WithMultipleVersions_ReturnsLatestOnly()
    {
        await _store.PublishAsync(MakeWorkflow("versioned", "1.0.0"));
        await _store.PublishAsync(MakeWorkflow("versioned", "2.0.0"));

        var catalog = await _store.CatalogAsync();
        catalog.Should().HaveCount(1);
        catalog[0].Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Catalog_FilterByTag_ReturnsMatching()
    {
        await _store.PublishAsync(MakeWorkflow("deploy-wf", tags: ["deploy", "prod"]));
        await _store.PublishAsync(MakeWorkflow("review-wf", tags: ["review", "code"]));

        var catalog = await _store.CatalogAsync(tag: "deploy");
        catalog.Should().HaveCount(1);
        catalog[0].Name.Should().Be("deploy-wf");
    }

    [Fact]
    public async Task Catalog_FilterByAuthor_ReturnsMatching()
    {
        await _store.PublishAsync(new SharedWorkflow
        {
            Name = "alice-wf",
            Version = "1.0.0",
            Author = "alice",
        });
        await _store.PublishAsync(new SharedWorkflow
        {
            Name = "bob-wf",
            Version = "1.0.0",
            Author = "bob",
        });

        var catalog = await _store.CatalogAsync(author: "alice");
        catalog.Should().HaveCount(1);
        catalog[0].Name.Should().Be("alice-wf");
    }

    [Fact]
    public async Task Catalog_EmptyStore_ReturnsEmpty()
    {
        var catalog = await _store.CatalogAsync();
        catalog.Should().BeEmpty();
    }

    // ── Search ────────────────────────────────────────────────

    [Fact]
    public async Task Search_ByName_ReturnsMatching()
    {
        await _store.PublishAsync(MakeWorkflow("deploy-pipeline", description: "Deploys apps"));
        await _store.PublishAsync(MakeWorkflow("code-review", description: "Reviews code"));

        var results = await _store.SearchAsync("deploy");
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("deploy-pipeline");
    }

    [Fact]
    public async Task Search_ByDescription_ReturnsMatching()
    {
        await _store.PublishAsync(MakeWorkflow("wf-a", description: "Automates kubernetes deployments"));
        await _store.PublishAsync(MakeWorkflow("wf-b", description: "Runs unit tests"));

        var results = await _store.SearchAsync("kubernetes");
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("wf-a");
    }

    [Fact]
    public async Task Search_ByTag_ReturnsMatching()
    {
        await _store.PublishAsync(MakeWorkflow("wf-tagged", tags: ["automation", "ci"]));
        await _store.PublishAsync(MakeWorkflow("wf-other", tags: ["review"]));

        var results = await _store.SearchAsync("automation");
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("wf-tagged");
    }

    [Fact]
    public async Task Search_CaseInsensitive_ReturnsMatching()
    {
        await _store.PublishAsync(MakeWorkflow("Deploy-Pipeline"));

        var results = await _store.SearchAsync("DEPLOY");
        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEmpty()
    {
        await _store.PublishAsync(MakeWorkflow("alpha"));

        var results = await _store.SearchAsync("xyz-nonexistent-query");
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_FindsAcrossMultipleVersions()
    {
        await _store.PublishAsync(MakeWorkflow("deploy", "1.0.0", description: "First version"));
        await _store.PublishAsync(MakeWorkflow("deploy", "2.0.0", description: "Second version"));

        // Search across all files (including all versions)
        var results = await _store.SearchAsync("deploy");
        results.Should().HaveCount(2);
    }

    // ── Versions ──────────────────────────────────────────────

    [Fact]
    public async Task Versions_ListsAllVersionsForName()
    {
        await _store.PublishAsync(MakeWorkflow("versioned-wf", "1.0.0"));
        await _store.PublishAsync(MakeWorkflow("versioned-wf", "1.1.0"));
        await _store.PublishAsync(MakeWorkflow("versioned-wf", "2.0.0"));

        var versions = await _store.VersionsAsync("versioned-wf");
        versions.Should().HaveCount(3);
        versions.Select(w => w.Version).Should().BeEquivalentTo(["1.0.0", "1.1.0", "2.0.0"]);
    }

    [Fact]
    public async Task Versions_UnknownName_ReturnsEmpty()
    {
        var versions = await _store.VersionsAsync("nonexistent-workflow");
        versions.Should().BeEmpty();
    }

    [Fact]
    public async Task Versions_DoesNotReturnOtherWorkflows()
    {
        await _store.PublishAsync(MakeWorkflow("workflow-a", "1.0.0"));
        await _store.PublishAsync(MakeWorkflow("workflow-b", "1.0.0"));

        var versions = await _store.VersionsAsync("workflow-a");
        versions.Should().HaveCount(1);
        versions[0].Name.Should().Be("workflow-a");
    }

    // ── Install ───────────────────────────────────────────────

    [Fact]
    public async Task Install_CopiesWorkflowToLocalDirectory()
    {
        var installDir = Path.Combine(_tempDir, "installed");
        await _store.PublishAsync(MakeWorkflow("installable", "1.0.0"));

        var result = await _store.InstallAsync("installable", "1.0.0", installDir);

        result.Should().BeTrue();
        Directory.Exists(installDir).Should().BeTrue();
        Directory.GetFiles(installDir, "*.json").Should().HaveCount(1);
    }

    [Fact]
    public async Task Install_CreatesLocalDirectory_IfNotExists()
    {
        var installDir = Path.Combine(_tempDir, "new-install-dir");
        await _store.PublishAsync(MakeWorkflow("installable2", "1.0.0"));

        Directory.Exists(installDir).Should().BeFalse();

        await _store.InstallAsync("installable2", "1.0.0", installDir);

        Directory.Exists(installDir).Should().BeTrue();
    }

    [Fact]
    public async Task Install_NonExistentWorkflow_ReturnsFalse()
    {
        var installDir = Path.Combine(_tempDir, "install-fail");

        var result = await _store.InstallAsync("nonexistent", "1.0.0", installDir);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Install_InstalledFileContainsWorkflowJson()
    {
        var installDir = Path.Combine(_tempDir, "install-verify");
        var workflow = new SharedWorkflow
        {
            Name = "verify-wf",
            Version = "1.0.0",
            Description = "Verify install content",
            DefinitionJson = "{\"verified\":true}",
        };
        await _store.PublishAsync(workflow);

        await _store.InstallAsync("verify-wf", "1.0.0", installDir);

        var installedFile = Directory.GetFiles(installDir, "*.json").Single();
        var content = await File.ReadAllTextAsync(installedFile);
        // InstallAsync writes DefinitionJson when available (local catalog format)
        content.Should().Contain("verified");
    }

    // ── Get ───────────────────────────────────────────────────

    [Fact]
    public async Task Get_ByNameAndVersion_ReturnsWorkflow()
    {
        await _store.PublishAsync(MakeWorkflow("get-test", "1.0.0"));

        var result = await _store.GetAsync("get-test", "1.0.0");

        result.Should().NotBeNull();
        result!.Name.Should().Be("get-test");
        result.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Get_ByName_ReturnsLatestVersion()
    {
        await _store.PublishAsync(MakeWorkflow("latest-test", "1.0.0"));
        await _store.PublishAsync(MakeWorkflow("latest-test", "2.0.0"));

        var result = await _store.GetAsync("latest-test");

        result.Should().NotBeNull();
        result!.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Get_NonExistentName_ReturnsNull()
    {
        var result = await _store.GetAsync("does-not-exist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_NonExistentVersion_ReturnsNull()
    {
        await _store.PublishAsync(MakeWorkflow("exists", "1.0.0"));

        var result = await _store.GetAsync("exists", "99.0.0");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_ById_ReturnsWorkflow()
    {
        var workflow = new SharedWorkflow
        {
            Id = "abc1234567890123",
            Name = "by-id-test",
            Version = "1.0.0",
        };
        await _store.PublishAsync(workflow);

        var result = await _store.GetAsync("abc1234567890123");

        result.Should().NotBeNull();
        result!.Name.Should().Be("by-id-test");
    }
}
