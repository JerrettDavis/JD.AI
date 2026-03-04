using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public class SkillParityToolsTests
{
    // ── skills_parity_matrix ─────────────────────────────────

    [Fact]
    public void ParityMatrix_All_ReturnsFullTable()
    {
        var result = SkillParityTools.GenerateParityMatrix();

        Assert.Contains("## OpenClaw → JD.AI Skills Parity Matrix", result);
        Assert.Contains("| # |", result);
        Assert.Contains("`github`", result);
        Assert.Contains("`discord`", result);
        Assert.Contains("native", result);
    }

    [Fact]
    public void ParityMatrix_FilterNative_OnlyShowsNative()
    {
        var result = SkillParityTools.GenerateParityMatrix("native");

        Assert.Contains("`github`", result);
        Assert.DoesNotContain("`trello`", result); // trello is planned
    }

    [Fact]
    public void ParityMatrix_FilterPlanned_OnlyShowsPlanned()
    {
        var result = SkillParityTools.GenerateParityMatrix("planned");

        Assert.Contains("`trello`", result);
        Assert.DoesNotContain("`github`", result); // github is native
    }

    [Fact]
    public void ParityMatrix_IncludesSummary()
    {
        var result = SkillParityTools.GenerateParityMatrix();

        Assert.Contains("### Summary", result);
        Assert.Contains("**native**:", result);
        Assert.Contains("**Coverage**:", result);
    }

    [Fact]
    public void ParityMatrix_IncludesNotApplicable()
    {
        var result = SkillParityTools.GenerateParityMatrix();

        Assert.Contains("not-applicable", result);
        Assert.Contains("`apple-notes`", result);
    }

    // ── skills_pack_overview ─────────────────────────────────

    [Fact]
    public void PackOverview_ShowsAllPacks()
    {
        var result = SkillParityTools.GetPackOverview();

        Assert.Contains("## JD.AI Skill Packs Overview", result);
        Assert.Contains("### Dev/Coding", result);
        Assert.Contains("### Communication", result);
        Assert.Contains("### Media", result);
        Assert.Contains("### Platform", result);
    }

    [Fact]
    public void PackOverview_ShowsCoveragePercentage()
    {
        var result = SkillParityTools.GetPackOverview();

        // Dev/Coding should have high coverage (github, coding-agent are native)
        Assert.Matches(@"\d+% coverage", result);
    }

    [Fact]
    public void PackOverview_ShowsBreakdown()
    {
        var result = SkillParityTools.GetPackOverview();

        Assert.Contains("Native:", result);
        Assert.Contains("Planned:", result);
    }

    // ── skills_gap_analysis ──────────────────────────────────

    [Fact]
    public void GapAnalysis_ShowsPlannedSkills()
    {
        var result = SkillParityTools.GetGapAnalysis();

        Assert.Contains("## Skills Gap Analysis", result);
        Assert.Contains("`trello`", result);
        Assert.Contains("`notion`", result);
    }

    [Fact]
    public void GapAnalysis_GroupsByPriority()
    {
        var result = SkillParityTools.GetGapAnalysis();

        Assert.Contains("### Priority 1", result);
        Assert.Contains("### Priority 2", result);
    }

    [Fact]
    public void GapAnalysis_ShowsEffortSummary()
    {
        var result = SkillParityTools.GetGapAnalysis();

        Assert.Contains("### Implementation Effort", result);
        Assert.Contains("**Total gaps**:", result);
        Assert.Contains("**P1 (high)**:", result);
    }

    // ── skills_detail ────────────────────────────────────────

    [Fact]
    public void Detail_KnownSkill_ShowsInfo()
    {
        var result = SkillParityTools.GetSkillDetail("github");

        Assert.Contains("## Skill Detail: github", result);
        Assert.Contains("Dev/Coding", result);
        Assert.Contains("native", result);
        Assert.Contains("GitHubTools", result);
    }

    [Fact]
    public void Detail_UnknownSkill_ReturnsError()
    {
        var result = SkillParityTools.GetSkillDetail("nonexistent-skill");

        Assert.Contains("Unknown skill", result);
        Assert.Contains("skills_parity_matrix", result);
    }

    [Fact]
    public void Detail_SkillWithSecurity_ShowsSecurityNotes()
    {
        var result = SkillParityTools.GetSkillDetail("trello");

        Assert.Contains("### Security / Governance", result);
        Assert.Contains("OAuth", result);
    }

    [Fact]
    public void Detail_SkillWithHint_ShowsImplementationHint()
    {
        var result = SkillParityTools.GetSkillDetail("trello");

        Assert.Contains("### Implementation Hint", result);
        Assert.Contains("REST API", result);
    }

    [Fact]
    public void Detail_CaseInsensitive()
    {
        var result = SkillParityTools.GetSkillDetail("GITHUB");

        Assert.Contains("## Skill Detail: github", result);
        Assert.Contains("native", result);
    }

    // ── skills_parity_export ─────────────────────────────────

    [Fact]
    public void Export_ReturnsValidJson()
    {
        var json = SkillParityTools.ExportParity();

        Assert.Contains("\"timestamp\"", json);
        Assert.Contains("\"totalSkills\"", json);
        Assert.Contains("\"summary\"", json);
        Assert.Contains("\"skills\"", json);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Export_IncludesSummaryCounts()
    {
        var json = SkillParityTools.ExportParity();

        Assert.Contains("\"native\":", json);
        Assert.Contains("\"planned\":", json);
        Assert.Contains("\"superseded\":", json);
        Assert.Contains("\"notApplicable\":", json);
    }

    [Fact]
    public void Export_IncludesAllSkills()
    {
        var json = SkillParityTools.ExportParity();

        // Should include some known skills
        Assert.Contains("\"github\"", json);
        Assert.Contains("\"discord\"", json);
        Assert.Contains("\"trello\"", json);
    }
}
