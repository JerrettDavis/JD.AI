using System.Globalization;
using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using Xunit;

namespace JD.AI.Tests.Agents;

public sealed class FileAgentDefinitionRegistryTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileAgentDefinitionRegistry _registry;

    public FileAgentDefinitionRegistryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _registry = new FileAgentDefinitionRegistry(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    // ── RegisterAsync / ResolveAsync ──────────────────────────────────────

    [Fact]
    public async Task Register_ThenResolve_ReturnsSameDefinition()
    {
        var def = MakeDef("pr-reviewer", "1.0");
        await _registry.RegisterAsync(def, AgentEnvironments.Dev);

        var resolved = await _registry.ResolveAsync("pr-reviewer", "1.0", AgentEnvironments.Dev);
        Assert.NotNull(resolved);
        Assert.Equal("pr-reviewer", resolved.Name);
        Assert.Equal("1.0", resolved.Version);
    }

    [Fact]
    public async Task Resolve_Latest_ReturnHighestSemVer()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "2.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "1.5"), AgentEnvironments.Dev);

        var resolved = await _registry.ResolveAsync("agent", "latest", AgentEnvironments.Dev);
        Assert.NotNull(resolved);
        Assert.Equal("2.0", resolved.Version);
    }

    [Fact]
    public async Task Resolve_Latest_PrefersNormalizedThreePartVersionForEquivalentSemVer()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "1.0.0"), AgentEnvironments.Dev);

        var resolved = await _registry.ResolveAsync("agent", "latest", AgentEnvironments.Dev);

        Assert.NotNull(resolved);
        Assert.Equal("1.0.0", resolved.Version);
    }

    [Fact]
    public async Task Resolve_NullVersion_ReturnsLatest()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "3.0"), AgentEnvironments.Dev);

        var resolved = await _registry.ResolveAsync("agent", null, AgentEnvironments.Dev);
        Assert.Equal("3.0", resolved?.Version);
    }

    [Fact]
    public async Task Resolve_UnknownName_ReturnsNull()
    {
        var resolved = await _registry.ResolveAsync("nonexistent", null, AgentEnvironments.Dev);
        Assert.Null(resolved);
    }

    [Fact]
    public async Task Register_InvalidKeyCharacters_Throws()
    {
        var definition = MakeDef("agent/name", "1.0");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _registry.RegisterAsync(definition, AgentEnvironments.Dev));

        Assert.Contains("Only letters, digits, '.', and '-' are allowed.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_InvalidVersionFormat_Throws()
    {
        var definition = MakeDef("agent", "1.0-beta");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _registry.RegisterAsync(definition, AgentEnvironments.Dev));

        Assert.Contains("Version must use numeric dot-separated components", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolve_WrongVersion_ReturnsNull()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        var resolved = await _registry.ResolveAsync("agent", "9.9", AgentEnvironments.Dev);
        Assert.Null(resolved);
    }

    // ── Environment isolation ──────────────────────────────────────────────

    [Fact]
    public async Task Environments_AreIsolated()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);

        var inStaging = await _registry.ResolveAsync("agent", null, AgentEnvironments.Staging);
        Assert.Null(inStaging);
    }

    [Fact]
    public async Task ListAsync_InvalidEnvironment_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _registry.ListAsync("..\\prod"));

        Assert.Contains("Environment must be one of:", ex.Message, StringComparison.Ordinal);
    }

    // ── ListAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsAllInEnvironment()
    {
        await _registry.RegisterAsync(MakeDef("a", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("b", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("c", "1.0"), AgentEnvironments.Staging);

        var devList = await _registry.ListAsync(AgentEnvironments.Dev);
        Assert.Equal(2, devList.Count);

        var stagingList = await _registry.ListAsync(AgentEnvironments.Staging);
        Assert.Single(stagingList);
    }

    [Fact]
    public async Task ListAsync_EmptyEnvironment_ReturnsEmpty()
    {
        var list = await _registry.ListAsync(AgentEnvironments.Prod);
        Assert.Empty(list);
    }

    // ── UnregisterAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Unregister_RemovesDefinition()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.UnregisterAsync("agent", "1.0", AgentEnvironments.Dev);

        var resolved = await _registry.ResolveAsync("agent", "1.0", AgentEnvironments.Dev);
        Assert.Null(resolved);
        Assert.Null(_registry.GetByName("agent"));
    }

    [Fact]
    public async Task Unregister_RemovingLatestDevVersion_FallsBackToRemainingVersion()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "2.0"), AgentEnvironments.Dev);

        await _registry.UnregisterAsync("agent", "2.0", AgentEnvironments.Dev);

        var cached = _registry.GetByName("agent");
        Assert.NotNull(cached);
        Assert.Equal("1.0", cached.Version);
    }

    [Fact]
    public async Task NonDevRegistrationsAndDeletes_DoNotMutateSyncCache()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "2.0"), AgentEnvironments.Staging);

        Assert.Equal("1.0", _registry.GetByName("agent")?.Version);

        await _registry.UnregisterAsync("agent", "2.0", AgentEnvironments.Staging);

        Assert.Equal("1.0", _registry.GetByName("agent")?.Version);
    }

    [Fact]
    public async Task Unregister_NonExistent_DoesNotThrow()
    {
        // Should complete without exception
        await _registry.UnregisterAsync("nonexistent", "99.0", AgentEnvironments.Dev);
    }

    // ── PromoteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Promote_CopiesDefinitionToHigherEnvironment()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.PromoteAsync("agent", "1.0", AgentEnvironments.Dev, AgentEnvironments.Staging);

        var inStaging = await _registry.ResolveAsync("agent", "1.0", AgentEnvironments.Staging);
        Assert.NotNull(inStaging);
        Assert.Equal("agent", inStaging.Name);
    }

    [Fact]
    public async Task Promote_StillExistsInSource()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.PromoteAsync("agent", "1.0", AgentEnvironments.Dev, AgentEnvironments.Staging);

        var inDev = await _registry.ResolveAsync("agent", "1.0", AgentEnvironments.Dev);
        Assert.NotNull(inDev);
    }

    [Fact]
    public async Task Promote_NonExistent_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _registry.PromoteAsync("nonexistent", "1.0", AgentEnvironments.Dev, AgentEnvironments.Staging));
    }

    [Fact]
    public async Task Promote_ReverseEnvironmentPath_Throws()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Staging);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _registry.PromoteAsync("agent", "1.0", AgentEnvironments.Staging, AgentEnvironments.Dev));

        Assert.Contains("Invalid promotion path: 'staging' can only promote to 'prod'.", ex.Message, StringComparison.Ordinal);
    }

    // ── Checksum verification ─────────────────────────────────────────────

    [Fact]
    public async Task Checksum_TamperedFile_SkippedOnLoad()
    {
        var def = MakeDef("agent", "1.0");
        await _registry.RegisterAsync(def, AgentEnvironments.Dev);

        // Tamper with the YAML file
        var dir = Path.Combine(_tempRoot, AgentEnvironments.Dev);
        var yamlFile = Directory.GetFiles(dir, "*.agent.yaml").Single();
        await File.AppendAllTextAsync(yamlFile, "\n# tampered");

        // Fresh registry reads from disk
        var freshRegistry = new FileAgentDefinitionRegistry(_tempRoot);
        var list = await freshRegistry.ListAsync(AgentEnvironments.Dev);

        // Tampered file should be skipped
        Assert.Empty(list);
    }

    [Fact]
    public async Task MissingChecksumFile_SkippedOnLoad()
    {
        var def = MakeDef("agent", "1.0");
        await _registry.RegisterAsync(def, AgentEnvironments.Dev);

        var dir = Path.Combine(_tempRoot, AgentEnvironments.Dev);
        var checksumFile = Directory.GetFiles(dir, "*.sha256").Single();
        File.Delete(checksumFile);

        var freshRegistry = new FileAgentDefinitionRegistry(_tempRoot);
        var list = await freshRegistry.ListAsync(AgentEnvironments.Dev);

        Assert.Empty(list);
    }

    [Fact]
    public async Task InvalidEnvironmentMetadata_SkippedOnLoad()
    {
        var dir = Path.Combine(_tempRoot, AgentEnvironments.Dev);
        Directory.CreateDirectory(dir);

        var yaml = """
            name: agent
            version: 1.0
            environment: ..\prod
            description: invalid environment
            """;

        var yamlPath = Path.Combine(dir, "agent@1.0.agent.yaml");
        await File.WriteAllTextAsync(yamlPath, yaml);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "agent@1.0.sha256"),
            string.Concat(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(yaml))
                .Select(static b => b.ToString("x2", CultureInfo.InvariantCulture))));

        var freshRegistry = new FileAgentDefinitionRegistry(_tempRoot);
        var list = await freshRegistry.ListAsync(AgentEnvironments.Dev);

        Assert.Empty(list);
    }

    [Fact]
    public async Task FreshRegistry_PreloadsLatestDevDefinitionIntoSyncCache()
    {
        await _registry.RegisterAsync(MakeDef("agent", "1.0"), AgentEnvironments.Dev);
        await _registry.RegisterAsync(MakeDef("agent", "2.0"), AgentEnvironments.Dev);

        var freshRegistry = new FileAgentDefinitionRegistry(_tempRoot);

        var found = freshRegistry.GetByName("agent");
        Assert.NotNull(found);
        Assert.Equal("2.0", found.Version);
    }

    // ── AgentEnvironments ─────────────────────────────────────────────────

    [Theory]
    [InlineData("dev", "staging")]
    [InlineData("staging", "prod")]
    [InlineData("prod", null)]
    public void NextAfter_ReturnsCorrectEnvironment(string from, string? expected)
    {
        Assert.Equal(expected, AgentEnvironments.NextAfter(from));
    }

    [Fact]
    public void All_ContainsAllThreeEnvironments()
    {
        Assert.Contains(AgentEnvironments.Dev, AgentEnvironments.All);
        Assert.Contains(AgentEnvironments.Staging, AgentEnvironments.All);
        Assert.Contains(AgentEnvironments.Prod, AgentEnvironments.All);
    }

    // ── IAgentDefinitionRegistry sync interface (memory cache) ────────────

    [Fact]
    public void SyncRegister_GetByName_Works()
    {
        var def = MakeDef("sync-agent", "1.0");
        _registry.Register(def);

        var found = _registry.GetByName("sync-agent");
        Assert.NotNull(found);
    }

    [Fact]
    public void GetByTag_ReturnsMatchingDefinitions()
    {
        var d1 = MakeDef("a", "1.0");
        d1.Tags.Add("code-review");
        var d2 = MakeDef("b", "1.0");
        d2.Tags.Add("code-review");
        var d3 = MakeDef("c", "1.0");
        d3.Tags.Add("documentation");

        _registry.Register(d1);
        _registry.Register(d2);
        _registry.Register(d3);

        var tagged = _registry.GetByTag("code-review");
        Assert.Equal(2, tagged.Count);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static AgentDefinition MakeDef(string name, string version) => new()
    {
        Name = name,
        Version = version,
        Description = $"Test agent: {name}",
        Model = new AgentModelSpec { Provider = "ClaudeCode", Id = "claude-opus-4" },
    };
}
