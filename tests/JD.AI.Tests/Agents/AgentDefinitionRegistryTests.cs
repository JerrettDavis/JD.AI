using JD.AI.Core.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JD.AI.Tests.Agents;

public sealed class AgentDefinitionRegistryTests : IDisposable
{
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);

    public AgentDefinitionRegistryTests() => Directory.CreateDirectory(_tmpDir);
    public void Dispose() { try { Directory.Delete(_tmpDir, recursive: true); } catch { } }

    // ── Registry tests ──────────────────────────────────────────────────────

    [Fact]
    public void Register_AddsDefinition()
    {
        var registry = new AgentDefinitionRegistry();
        registry.Register(new AgentDefinition { Name = "my-agent" });
        Assert.Single(registry.GetAll());
    }

    [Fact]
    public void Register_OverwritesExistingName()
    {
        var registry = new AgentDefinitionRegistry();
        registry.Register(new AgentDefinition { Name = "alpha", Version = "1.0" });
        registry.Register(new AgentDefinition { Name = "alpha", Version = "2.0" });
        var def = registry.GetByName("alpha");
        Assert.NotNull(def);
        Assert.Equal("2.0", def.Version);
    }

    [Fact]
    public void GetByName_CaseInsensitive()
    {
        var registry = new AgentDefinitionRegistry();
        registry.Register(new AgentDefinition { Name = "MyAgent" });
        Assert.NotNull(registry.GetByName("myagent"));
        Assert.NotNull(registry.GetByName("MYAGENT"));
    }

    [Fact]
    public void GetByName_ReturnsNullWhenNotFound()
    {
        var registry = new AgentDefinitionRegistry();
        Assert.Null(registry.GetByName("nope"));
    }

    [Fact]
    public void GetByTag_ReturnsMatchingDefinitions()
    {
        var registry = new AgentDefinitionRegistry();
        registry.Register(new AgentDefinition { Name = "a", Tags = ["code-review", "security"] });
        registry.Register(new AgentDefinition { Name = "b", Tags = ["docs"] });
        registry.Register(new AgentDefinition { Name = "c", Tags = ["code-review"] });

        var results = registry.GetByTag("code-review");
        Assert.Equal(2, results.Count);
        Assert.Contains(results, d => string.Equals(d.Name, "a", StringComparison.Ordinal));
        Assert.Contains(results, d => string.Equals(d.Name, "c", StringComparison.Ordinal));
    }

    [Fact]
    public void GetByTag_CaseInsensitive()
    {
        var registry = new AgentDefinitionRegistry();
        registry.Register(new AgentDefinition { Name = "x", Tags = ["CodeReview"] });
        Assert.Single(registry.GetByTag("codereview"));
    }

    [Fact]
    public void Register_ThrowsOnEmptyName()
    {
        var registry = new AgentDefinitionRegistry();
        Assert.Throws<ArgumentException>(() =>
            registry.Register(new AgentDefinition { Name = "" }));
    }

    // ── Loader tests ─────────────────────────────────────────────────────────

    [Fact]
    public void Loader_LoadsValidYamlFile()
    {
        var yaml = """
            name: pr-reviewer
            displayName: PR Reviewer
            version: "1.2"
            description: Reviews pull requests
            loadout: developer
            tags:
              - code-review
              - security
            workflows:
              - pr-workflow
            """;

        File.WriteAllText(Path.Combine(_tmpDir, "pr-reviewer.agent.yaml"), yaml);

        var registry = new AgentDefinitionRegistry();
        var loader = new FileAgentDefinitionLoader(registry, NullLogger<FileAgentDefinitionLoader>.Instance);
        loader.LoadAll([_tmpDir]);

        var def = registry.GetByName("pr-reviewer");
        Assert.NotNull(def);
        Assert.Equal("PR Reviewer", def.DisplayName);
        Assert.Equal("1.2", def.Version);
        Assert.Contains("code-review", def.Tags);
        Assert.Contains("pr-workflow", def.Workflows);
    }

    [Fact]
    public void Loader_SkipsMissingDirectory()
    {
        var registry = new AgentDefinitionRegistry();
        var loader = new FileAgentDefinitionLoader(registry, NullLogger<FileAgentDefinitionLoader>.Instance);
        // Should not throw
        loader.LoadAll(["/nonexistent/path/agents"]);
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Loader_SkipsMalformedYaml()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "bad.agent.yaml"), "{ not valid yaml: [[[");
        var registry = new AgentDefinitionRegistry();
        var loader = new FileAgentDefinitionLoader(registry, NullLogger<FileAgentDefinitionLoader>.Instance);
        // Should not throw
        loader.LoadAll([_tmpDir]);
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Loader_SkipsFileWithNoName()
    {
        var yaml = """
            displayName: No Name Agent
            version: "1.0"
            """;
        File.WriteAllText(Path.Combine(_tmpDir, "noname.agent.yaml"), yaml);

        var registry = new AgentDefinitionRegistry();
        var loader = new FileAgentDefinitionLoader(registry, NullLogger<FileAgentDefinitionLoader>.Instance);
        loader.LoadAll([_tmpDir]);
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Loader_LoadsMultipleFiles()
    {
        File.WriteAllText(Path.Combine(_tmpDir, "a.agent.yaml"), "name: agent-a\nversion: \"1.0\"");
        File.WriteAllText(Path.Combine(_tmpDir, "b.agent.yaml"), "name: agent-b\nversion: \"1.0\"");

        var registry = new AgentDefinitionRegistry();
        var loader = new FileAgentDefinitionLoader(registry, NullLogger<FileAgentDefinitionLoader>.Instance);
        loader.LoadAll([_tmpDir]);
        Assert.Equal(2, registry.GetAll().Count);
    }

    [Fact]
    public void Loader_LoadsModelSpec()
    {
        var yaml = """
            name: smart-agent
            model:
              provider: ClaudeCode
              id: claude-opus-4
              maxOutputTokens: 8096
              temperature: 0.7
            """;
        File.WriteAllText(Path.Combine(_tmpDir, "smart.agent.yaml"), yaml);

        var registry = new AgentDefinitionRegistry();
        var loader = new FileAgentDefinitionLoader(registry, NullLogger<FileAgentDefinitionLoader>.Instance);
        loader.LoadAll([_tmpDir]);

        var def = registry.GetByName("smart-agent");
        Assert.NotNull(def?.Model);
        Assert.Equal("ClaudeCode", def.Model.Provider);
        Assert.Equal("claude-opus-4", def.Model.Id);
        Assert.Equal(8096, def.Model.MaxOutputTokens);
        Assert.Equal(0.7, def.Model.Temperature);
    }
}
