using JD.AI.Tui.Agent;

namespace JD.AI.Tui.Tests;

public sealed class InstructionsLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public InstructionsLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    [Fact]
    public void Load_NoFiles_ReturnsEmptyResult()
    {
        var result = InstructionsLoader.Load(_tempDir);

        Assert.False(result.HasInstructions);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Load_JdaiMd_Found()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "# Project rules\nBe concise.");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.True(result.HasInstructions);
        Assert.Single(result.Files);
        Assert.Equal("JDAI.md", result.Files[0].Name);
        Assert.Contains("Be concise", result.Files[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ClaudeMd_Found()
    {
        File.WriteAllText(Path.Combine(_tempDir, "CLAUDE.md"), "# Claude instructions");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.True(result.HasInstructions);
        Assert.Equal("CLAUDE.md", result.Files[0].Name);
    }

    [Fact]
    public void Load_AgentsMd_Found()
    {
        File.WriteAllText(Path.Combine(_tempDir, "AGENTS.md"), "# Agents config");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.Equal("AGENTS.md", result.Files[0].Name);
    }

    [Fact]
    public void Load_MultipleSources_MergesAll()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "Rule 1");
        File.WriteAllText(Path.Combine(_tempDir, "CLAUDE.md"), "Rule 2");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.Equal(2, result.Files.Count);
        Assert.Equal("JDAI.md", result.Files[0].Name);
        Assert.Equal("CLAUDE.md", result.Files[1].Name);
    }

    [Fact]
    public void Load_EmptyFile_IsSkipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "   ");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.False(result.HasInstructions);
    }

    [Fact]
    public void Load_IncludeDirective_ExpandsContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "Base rules\ninclude: extra.md\nMore rules");
        File.WriteAllText(Path.Combine(_tempDir, "extra.md"), "Included content");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.Contains("Included content", result.Files[0].Content, StringComparison.Ordinal);
        Assert.Contains("Base rules", result.Files[0].Content, StringComparison.Ordinal);
        Assert.Contains("More rules", result.Files[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_IncludeDirective_MissingFile_ReplacedWithComment()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "Base\ninclude: nonexistent.md\nEnd");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.Contains("include not found", result.Files[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSystemPrompt_FormatsCorrectly()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "Rule A");
        File.WriteAllText(Path.Combine(_tempDir, "CLAUDE.md"), "Rule B");

        var result = InstructionsLoader.Load(_tempDir);
        var prompt = result.ToSystemPrompt();

        Assert.Contains("JDAI.md", prompt, StringComparison.Ordinal);
        Assert.Contains("Rule A", prompt, StringComparison.Ordinal);
        Assert.Contains("CLAUDE.md", prompt, StringComparison.Ordinal);
        Assert.Contains("Rule B", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSummary_ShowsFileList()
    {
        File.WriteAllText(Path.Combine(_tempDir, "JDAI.md"), "Some rules");

        var result = InstructionsLoader.Load(_tempDir);
        var summary = result.ToSummary();

        Assert.Contains("JDAI.md", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDirectoryChain_StopsAtGitRoot()
    {
        // Create a nested directory structure with a .git dir at root
        var gitDir = Path.Combine(_tempDir, ".git");
        Directory.CreateDirectory(gitDir);
        var nested = Path.Combine(_tempDir, "src", "deep");
        Directory.CreateDirectory(nested);

        var chain = InstructionsLoader.GetDirectoryChain(nested);

        Assert.Contains(_tempDir, chain);
        Assert.DoesNotContain(Directory.GetParent(_tempDir)?.FullName ?? "", chain);
    }

    [Fact]
    public void GetDirectoryChain_IncludesStartDir()
    {
        var chain = InstructionsLoader.GetDirectoryChain(_tempDir);
        Assert.Contains(_tempDir, chain);
    }

    [Fact]
    public void Load_CopilotInstructions_FromGithubDir()
    {
        var ghDir = Path.Combine(_tempDir, ".github");
        Directory.CreateDirectory(ghDir);
        File.WriteAllText(Path.Combine(ghDir, "copilot-instructions.md"), "Copilot rules");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.True(result.HasInstructions);
        Assert.Equal(".github/copilot-instructions.md", result.Files[0].Name);
    }

    [Fact]
    public void Load_JdaiInstructions_FromDotDir()
    {
        var jdDir = Path.Combine(_tempDir, ".jdai");
        Directory.CreateDirectory(jdDir);
        File.WriteAllText(Path.Combine(jdDir, "instructions.md"), "Dot dir instructions");

        var result = InstructionsLoader.Load(_tempDir);

        Assert.True(result.HasInstructions);
        Assert.Equal(".jdai/instructions.md", result.Files[0].Name);
    }
}
