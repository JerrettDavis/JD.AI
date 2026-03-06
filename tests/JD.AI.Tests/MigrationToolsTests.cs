using JD.AI.Core.Tools;
using JD.AI.Tests.Fixtures;

namespace JD.AI.Tests;

public class MigrationToolsTests : IDisposable
{
    private readonly TempDirectoryFixture _fixture = new();
    private readonly string _claudeDir;

    public MigrationToolsTests()
    {
        _claudeDir = Path.Combine(_fixture.DirectoryPath, ".claude");
        Directory.CreateDirectory(_claudeDir);
    }

    public void Dispose() => _fixture.Dispose();

    // ── migration_scan ───────────────────────────────────────

    [Fact]
    public void Scan_MissingDirectory_ReturnsNotFound()
    {
        var result = MigrationTools.ScanClaudeInstallation("/nonexistent/path");
        Assert.Contains("not found", result);
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsZeroCounts()
    {
        var result = MigrationTools.ScanClaudeInstallation(_claudeDir);

        Assert.Contains("## Claude Code Migration Scan", result);
        Assert.Contains("Skills (0)", result);
        Assert.Contains("Plugins (0)", result);
    }

    [Fact]
    public void Scan_WithSkills_ListsThem()
    {
        var skillDir = Path.Combine(_claudeDir, "skills", "brainstorming");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: brainstorming\ndescription: \"Creative exploration\"\n---\n# Brainstorming");

        var result = MigrationTools.ScanClaudeInstallation(_claudeDir);

        Assert.Contains("Skills (1)", result);
        Assert.Contains("`brainstorming`", result);
        Assert.Contains("Creative exploration", result);
    }

    [Fact]
    public void Scan_WithPlugins_ListsThem()
    {
        var pluginDir = Path.Combine(_claudeDir, "plugins", "my-plugin");
        Directory.CreateDirectory(pluginDir);
        File.WriteAllText(
            Path.Combine(pluginDir, "README.md"),
            "# My Plugin\nA helpful plugin");

        var result = MigrationTools.ScanClaudeInstallation(_claudeDir);

        Assert.Contains("Plugins (1)", result);
        Assert.Contains("`my-plugin`", result);
    }

    [Fact]
    public void Scan_WithClaudeMd_DetectsIt()
    {
        File.WriteAllText(
            Path.Combine(_claudeDir, "CLAUDE.md"),
            "# Project Instructions\nBe helpful.");

        var result = MigrationTools.ScanClaudeInstallation(_claudeDir);

        Assert.Contains("✓ CLAUDE.md found", result);
    }

    [Fact]
    public void Scan_WithoutClaudeMd_ReportsAbsent()
    {
        var result = MigrationTools.ScanClaudeInstallation(_claudeDir);
        Assert.Contains("✗ No CLAUDE.md", result);
    }

    // ── migration_analyze ────────────────────────────────────

    [Fact]
    public void Analyze_MissingSkill_ReportsNotFound()
    {
        var result = MigrationTools.AnalyzeSkill("nonexistent", _claudeDir);
        Assert.Contains("Could not find", result);
    }

    [Fact]
    public void Analyze_ExistingSkill_ShowsMetadata()
    {
        var skillDir = Path.Combine(_claudeDir, "skills", "test-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: test-skill\ndescription: \"Test skill\"\n---\n# Test Skill\n## Overview\nThis is a test.");

        var result = MigrationTools.AnalyzeSkill("test-skill", _claudeDir);

        Assert.Contains("## Migration Analysis: test-skill", result);
        Assert.Contains("**name**: test-skill", result);
        Assert.Contains("Prompt-based skill", result);
    }

    [Fact]
    public void Analyze_SkillWithToolUsage_WarnsAboutTools()
    {
        var skillDir = Path.Combine(_claudeDir, "skills", "code-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: code-skill\n---\n# Code\nUse the bash tool to run shell commands.");

        var result = MigrationTools.AnalyzeSkill("code-skill", _claudeDir);

        Assert.Contains("Has tool usage", result);
    }

    [Fact]
    public void Analyze_KnownSkill_ShowsMapping()
    {
        var skillDir = Path.Combine(_claudeDir, "skills", "brainstorming");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            "---\nname: brainstorming\n---\n# Brainstorming\n## Process");

        var result = MigrationTools.AnalyzeSkill("brainstorming", _claudeDir);

        Assert.Contains("### JD.AI Equivalent", result);
        Assert.Contains("native", result);
    }

    // ── migration_convert ────────────────────────────────────

    [Fact]
    public void Convert_ReplacesClaudeReferences()
    {
        var content = "This is a Claude Code project.\nSee CLAUDE.md for details.";
        var result = MigrationTools.ConvertInstructions(content);

        Assert.Contains("JD.AI", result);
        Assert.Contains("JDAI.md", result);
        Assert.DoesNotContain("Claude Code", result);
    }

    [Fact]
    public void Convert_FromFile_WorksCorrectly()
    {
        var filePath = Path.Combine(_fixture.DirectoryPath, "CLAUDE.md");
        File.WriteAllText(filePath, "Use claude code tools.\nSee CLAUDE.md.");

        var result = MigrationTools.ConvertInstructions(filePath);

        Assert.Contains("JD.AI", result);
        Assert.Contains("JDAI.md", result);
    }

    [Fact]
    public void Convert_AddsHeader()
    {
        var result = MigrationTools.ConvertInstructions("# Instructions");
        Assert.Contains("# JD.AI Project Instructions", result);
        Assert.Contains("Converted from CLAUDE.md", result);
    }

    // ── migration_parity ─────────────────────────────────────

    [Fact]
    public void Parity_ReturnsFormattedTable()
    {
        var result = MigrationTools.GenerateParityMatrix();

        Assert.Contains("## Skill Migration Matrix", result);
        Assert.Contains("| Source Skill |", result);
        Assert.Contains("`brainstorming`", result);
        Assert.Contains("native", result);
    }

    [Fact]
    public void Parity_IncludesStatusSummary()
    {
        var result = MigrationTools.GenerateParityMatrix();

        Assert.Contains("### Summary", result);
        Assert.Contains("**native**:", result);
    }

    [Fact]
    public void Parity_IncludesNotApplicable()
    {
        var result = MigrationTools.GenerateParityMatrix();

        Assert.Contains("not-applicable", result);
        Assert.Contains("macOS only", result);
    }

    // ── migration_export ─────────────────────────────────────

    [Fact]
    public void Export_ReturnsValidJson()
    {
        var json = MigrationTools.ExportScanResults(_claudeDir);

        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"skills\"", json);
        Assert.Contains("\"plugins\"", json);
        Assert.Contains("\"mappings\"", json);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Export_IncludesDiscoveredSkills()
    {
        var skillDir = Path.Combine(_claudeDir, "skills", "my-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: my-skill\n---\n# Test");

        var json = MigrationTools.ExportScanResults(_claudeDir);

        Assert.Contains("\"my-skill\"", json);
    }
}
