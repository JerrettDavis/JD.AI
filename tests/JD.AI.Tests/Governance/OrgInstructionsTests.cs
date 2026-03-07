using JD.AI.Core.Agents;
using JD.AI.Core.Config;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests.Governance;

[Collection("DataDirectories")]
public sealed class OrgInstructionsTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string _orgDir;
    private readonly string _projectDir;

    public OrgInstructionsTests()
    {
        _orgDir = Path.Combine(_fixture.DirectoryPath, "org-config");
        _projectDir = Path.Combine(_fixture.DirectoryPath, "project");

        Directory.CreateDirectory(_orgDir);
        Directory.CreateDirectory(_projectDir);

        DataDirectories.Reset();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", null);
        DataDirectories.Reset();
        _fixture.Dispose();
    }

    [Fact]
    public void Load_OrgConfigEnvVar_OrgInstructionsIncludedFirst()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "# Org rules");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);

        Assert.True(result.HasInstructions);
        Assert.Single(result.Files);
        Assert.Equal("org:JDAI.md", result.Files[0].Name);
        Assert.Contains("Org rules", result.Files[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_OrgConfigEnvVar_OrgInstructionsBeforeProjectInstructions()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "# Org rules");
        File.WriteAllText(Path.Combine(_projectDir, "JDAI.md"), "# Project rules");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);

        Assert.Equal(2, result.Files.Count);
        Assert.Equal("org:JDAI.md", result.Files[0].Name);
        Assert.Equal("JDAI.md", result.Files[1].Name);
        Assert.Contains("Org rules", result.Files[0].Content, StringComparison.Ordinal);
        Assert.Contains("Project rules", result.Files[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_OrgConfigEnvVar_OrgInstructionsFirstInSystemPrompt()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "Org policy content");
        File.WriteAllText(Path.Combine(_projectDir, "CLAUDE.md"), "Project content");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);
        var prompt = result.ToSystemPrompt();

        var orgIndex = prompt.IndexOf("Org policy content", StringComparison.Ordinal);
        var projectIndex = prompt.IndexOf("Project content", StringComparison.Ordinal);

        Assert.True(orgIndex < projectIndex, "Org instructions should appear before project instructions in the system prompt");
    }

    [Fact]
    public void Load_OrgConfigFromFile_UsedWhenEnvVarNotSet()
    {
        // Set up the DataDirectories root to our temp dir
        var jdaiRootDir = Path.Combine(_fixture.DirectoryPath, "jdai-root");
        Directory.CreateDirectory(jdaiRootDir);
        DataDirectories.SetRoot(jdaiRootDir);

        // Write the org-config-path file pointing to the org dir
        File.WriteAllText(Path.Combine(jdaiRootDir, "org-config-path"), _orgDir);
        File.WriteAllText(Path.Combine(_orgDir, "AGENTS.md"), "# Org agents config");

        var result = InstructionsLoader.Load(_projectDir);

        Assert.True(result.HasInstructions);
        Assert.Equal("org:AGENTS.md", result.Files[0].Name);
        Assert.Contains("Org agents config", result.Files[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NoOrgConfig_BehaviorUnchanged()
    {
        File.WriteAllText(Path.Combine(_projectDir, "JDAI.md"), "# Project only");
        // Ensure no env var is set
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", null);

        var result = InstructionsLoader.Load(_projectDir);

        Assert.Single(result.Files);
        Assert.Equal("JDAI.md", result.Files[0].Name);
        Assert.DoesNotContain(result.Files, f => f.Name.StartsWith("org:", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_OrgInstructions_UseOrgPrefix()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "# Org");
        File.WriteAllText(Path.Combine(_orgDir, "CLAUDE.md"), "# Org Claude");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);

        Assert.All(result.Files, f => Assert.StartsWith("org:", f.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Load_OrgConfigEnvVarPointsToNonExistentDir_OrgInstructionsSkipped()
    {
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", Path.Combine(_fixture.DirectoryPath, "nonexistent"));
        File.WriteAllText(Path.Combine(_projectDir, "JDAI.md"), "# Project rules");

        var result = InstructionsLoader.Load(_projectDir);

        Assert.Single(result.Files);
        Assert.Equal("JDAI.md", result.Files[0].Name);
    }

    [Fact]
    public void Load_OrgConfigEmptyFile_IsSkipped()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "   ");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);

        Assert.DoesNotContain(result.Files, f => f.Name.StartsWith("org:", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_OrgConfig_MultipleFileTypes_AllFoundWithOrgPrefix()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "Org JDAI");
        File.WriteAllText(Path.Combine(_orgDir, "CLAUDE.md"), "Org Claude");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);

        Assert.Equal(2, result.Files.Count);
        Assert.Equal("org:JDAI.md", result.Files[0].Name);
        Assert.Equal("org:CLAUDE.md", result.Files[1].Name);
    }

    [Fact]
    public void ToSystemPrompt_OrgInstructions_IncludesOrgFileName()
    {
        File.WriteAllText(Path.Combine(_orgDir, "JDAI.md"), "Org content here");
        Environment.SetEnvironmentVariable("JDAI_ORG_CONFIG", _orgDir);

        var result = InstructionsLoader.Load(_projectDir);
        var prompt = result.ToSystemPrompt();

        Assert.Contains("org:JDAI.md", prompt, StringComparison.Ordinal);
        Assert.Contains("Org content here", prompt, StringComparison.Ordinal);
    }
}
