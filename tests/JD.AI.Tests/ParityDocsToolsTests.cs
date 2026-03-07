using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public class ParityDocsToolsTests
{
    // ── parity_compatibility_matrix ──────────────────────────

    [Fact]
    public void CompatibilityMatrix_All_ReturnsFullTable()
    {
        var result = ParityDocsTools.GenerateCompatibilityMatrix();

        Assert.Contains("## JD.AI Capability Matrix", result);
        Assert.Contains("| Feature |", result);
        Assert.Contains("OpenClaw", result);
    }

    [Fact]
    public void CompatibilityMatrix_FilterTools_OnlyShowsTools()
    {
        var result = ParityDocsTools.GenerateCompatibilityMatrix("tools");

        Assert.Contains("File read/write/edit", result);
        Assert.DoesNotContain("Project instructions", result);
    }

    [Fact]
    public void CompatibilityMatrix_FilterGovernance_OnlyShowsGovernance()
    {
        var result = ParityDocsTools.GenerateCompatibilityMatrix("governance");

        Assert.Contains("Tool safety tiers", result);
        Assert.Contains("Policy-as-code", result);
    }

    [Fact]
    public void CompatibilityMatrix_IncludesScores()
    {
        var result = ParityDocsTools.GenerateCompatibilityMatrix();

        Assert.Contains("### Coverage Scores", result);
        Assert.Contains("**JD.AI**:", result);
    }

    // ── parity_migration_guide ───────────────────────────────

    [Fact]
    public void MigrationGuide_Claude_ShowsClaudeMapping()
    {
        var result = ParityDocsTools.GenerateMigrationGuide("claude");

        Assert.Contains("# Migrating from Claude Code to JD.AI", result);
        Assert.Contains("CLAUDE.md", result);
        Assert.Contains("JDAI.md", result);
        Assert.Contains("migration_scan", result);
    }

    [Fact]
    public void MigrationGuide_OpenClaw_ShowsOpenClawMapping()
    {
        var result = ParityDocsTools.GenerateMigrationGuide("openclaw");

        Assert.Contains("# Migrating from OpenClaw to JD.AI", result);
        Assert.Contains("openclaw.yaml", result);
    }

    [Fact]
    public void MigrationGuide_Copilot_ShowsCopilotMapping()
    {
        var result = ParityDocsTools.GenerateMigrationGuide("copilot");

        Assert.Contains("Copilot CLI", result);
        Assert.Contains("copilot-instructions.md", result);
    }

    [Fact]
    public void MigrationGuide_Codex_ShowsCodexMapping()
    {
        var result = ParityDocsTools.GenerateMigrationGuide("codex");

        Assert.Contains("Codex CLI", result);
        Assert.Contains("AGENTS.md", result);
    }

    [Fact]
    public void MigrationGuide_IncludesQuickStart()
    {
        var result = ParityDocsTools.GenerateMigrationGuide("claude");

        Assert.Contains("## Quick Start", result);
        Assert.Contains("dotnet tool install", result);
    }

    [Fact]
    public void MigrationGuide_IncludesWhatsNew()
    {
        var result = ParityDocsTools.GenerateMigrationGuide("claude");

        Assert.Contains("## What's Different in JD.AI", result);
        Assert.Contains("Multi-provider", result);
    }

    // ── parity_governance_runbook ────────────────────────────

    [Fact]
    public void GovernanceRunbook_Tools_ShowsSafetyTiers()
    {
        var result = ParityDocsTools.GenerateGovernanceRunbook("tools");

        Assert.Contains("Tool Safety Tiers", result);
        Assert.Contains("AutoApprove", result);
        Assert.Contains("AlwaysConfirm", result);
    }

    [Fact]
    public void GovernanceRunbook_Skills_ShowsTrustModel()
    {
        var result = ParityDocsTools.GenerateGovernanceRunbook("skills");

        Assert.Contains("Skill Trust Model", result);
        Assert.Contains("Built-in", result);
        Assert.Contains("Community", result);
    }

    [Fact]
    public void GovernanceRunbook_Mcp_ShowsSecurityChecklist()
    {
        var result = ParityDocsTools.GenerateGovernanceRunbook("mcp");

        Assert.Contains("MCP Security Checklist", result);
        Assert.Contains("TLS", result);
    }

    [Fact]
    public void GovernanceRunbook_Providers_ShowsCredentialInfo()
    {
        var result = ParityDocsTools.GenerateGovernanceRunbook("providers");

        Assert.Contains("Provider Credential Security", result);
        Assert.Contains("OPENAI_API_KEY", result);
    }

    [Fact]
    public void GovernanceRunbook_Channels_ShowsChannelSecurity()
    {
        var result = ParityDocsTools.GenerateGovernanceRunbook("channels");

        Assert.Contains("Channel Security", result);
        Assert.Contains("Discord", result);
    }

    [Fact]
    public void GovernanceRunbook_Unknown_ReturnsError()
    {
        var result = ParityDocsTools.GenerateGovernanceRunbook("unknown");

        Assert.Contains("Unknown area", result);
    }

    // ── parity_threat_model ──────────────────────────────────

    [Fact]
    public void ThreatModel_ToolExecution_ShowsThreats()
    {
        var result = ParityDocsTools.GenerateThreatModel("tool-execution");

        Assert.Contains("# Threat Model: tool-execution", result);
        Assert.Contains("Command injection", result);
        Assert.Contains("🔴 Critical", result);
    }

    [Fact]
    public void ThreatModel_SkillLoading_ShowsThreats()
    {
        var result = ParityDocsTools.GenerateThreatModel("skill-loading");

        Assert.Contains("Prompt injection", result);
    }

    [Fact]
    public void ThreatModel_Gateway_ShowsThreats()
    {
        var result = ParityDocsTools.GenerateThreatModel("gateway");

        Assert.Contains("Unauthenticated API access", result);
        Assert.Contains("DDoS", result);
    }

    [Fact]
    public void ThreatModel_IncludesRiskSummary()
    {
        var result = ParityDocsTools.GenerateThreatModel("tool-execution");

        Assert.Contains("### Risk Summary", result);
        Assert.Contains("Critical:", result);
    }

    [Fact]
    public void ThreatModel_Unknown_ReturnsError()
    {
        var result = ParityDocsTools.GenerateThreatModel("unknown-feature");

        Assert.Contains("Unknown feature", result);
    }

    // ── parity_export ────────────────────────────────────────

    [Fact]
    public void Export_ReturnsValidJson()
    {
        var json = ParityDocsTools.ExportParityData();

        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc);
        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"compatibility\"", json);
        Assert.Contains("\"scores\"", json);
    }

    [Fact]
    public void Export_IncludesScores()
    {
        var json = ParityDocsTools.ExportParityData();

        Assert.Contains("\"jdai\":", json);
        Assert.Contains("\"total\":", json);
    }
}
