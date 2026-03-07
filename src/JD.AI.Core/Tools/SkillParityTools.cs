using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using JD.AI.Core.Infrastructure;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for tracking and managing OpenClaw bundled-skill parity in JD.AI.
/// Provides a canonical parity matrix, pack grouping, and gap analysis.
/// </summary>
[ToolPlugin("skillParity")]
public sealed class SkillParityTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = JsonDefaults.Indented;

    // ── Parity Matrix ───────────────────────────────────────

    [KernelFunction("skills_parity_matrix")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate the complete OpenClaw→JD.AI skills parity matrix showing status for every bundled skill.")]
    public static string GenerateParityMatrix(
        [Description("Optional filter: 'native', 'planned', 'superseded', 'not-applicable', or 'all' (default)")] string? filter = null)
    {
        var skills = GetCanonicalSkills();

        if (!string.IsNullOrEmpty(filter) && !string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase))
            skills = skills.Where(s => string.Equals(s.Status, filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## OpenClaw → JD.AI Skills Parity Matrix");
        sb.AppendLine();
        sb.AppendLine("| # | OpenClaw Skill | Pack | Status | JD.AI Equivalent | Notes |");
        sb.AppendLine("|---|---------------|------|--------|-----------------|-------|");

        var index = 0;
        foreach (var skill in skills.OrderBy(s => s.Pack).ThenBy(s => s.Name))
        {
            index++;
            var icon = GetStatusIcon(skill.Status);
            sb.AppendLine(
                $"| {index} | `{skill.Name}` | {skill.Pack} | {icon} {skill.Status} | {skill.Equivalent ?? "—"} | {skill.Notes} |");
        }

        sb.AppendLine();
        AppendSummary(sb, GetCanonicalSkills());

        return sb.ToString();
    }

    // ── Pack Overview ───────────────────────────────────────

    [KernelFunction("skills_pack_overview")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Show skills grouped by pack (Productivity, Dev/Coding, Media, Device/OS, Platform, Communication) with coverage percentages.")]
    public static string GetPackOverview()
    {
        var skills = GetCanonicalSkills();
        var packs = skills.GroupBy(s => s.Pack).OrderBy(g => g.Key);

        var sb = new StringBuilder();
        sb.AppendLine("## JD.AI Skill Packs Overview");
        sb.AppendLine();

        foreach (var pack in packs)
        {
            var total = pack.Count();
            var native = pack.Count(s => string.Equals(s.Status, "native", StringComparison.OrdinalIgnoreCase));
            var planned = pack.Count(s => string.Equals(s.Status, "planned", StringComparison.OrdinalIgnoreCase));
            var superseded = pack.Count(s => string.Equals(s.Status, "superseded", StringComparison.OrdinalIgnoreCase));
            var na = pack.Count(s => string.Equals(s.Status, "not-applicable", StringComparison.OrdinalIgnoreCase));
            var actionable = total - na;
            var coverage = actionable > 0 ? (native + superseded) * 100 / actionable : 100;

            sb.AppendLine($"### {pack.Key} ({coverage}% coverage)");
            sb.AppendLine($"Native: {native} | Planned: {planned} | Superseded: {superseded} | N/A: {na} | Total: {total}");
            sb.AppendLine();

            foreach (var skill in pack.OrderBy(s => s.Name))
            {
                var icon = GetStatusIcon(skill.Status);
                sb.AppendLine($"- {icon} `{skill.Name}` → {skill.Equivalent ?? "—"}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Gap Analysis ────────────────────────────────────────

    [KernelFunction("skills_gap_analysis")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Identify skill gaps — planned skills not yet implemented, ranked by priority tier.")]
    public static string GetGapAnalysis()
    {
        var skills = GetCanonicalSkills()
            .Where(s => string.Equals(s.Status, "planned", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Pack)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## Skills Gap Analysis — Planned Implementations");
        sb.AppendLine();

        if (skills.Count == 0)
        {
            sb.AppendLine("🎉 No planned skills remaining — full parity achieved!");
            return sb.ToString();
        }

        var tiers = skills.GroupBy(s => s.Priority);
        foreach (var tier in tiers)
        {
            sb.AppendLine($"### Priority {tier.Key}");
            foreach (var skill in tier)
            {
                sb.AppendLine($"- `{skill.Name}` ({skill.Pack}) — {skill.Notes}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### Implementation Effort");
        sb.AppendLine($"- **Total gaps**: {skills.Count}");
        sb.AppendLine($"- **P1 (high)**: {skills.Count(s => s.Priority == 1)}");
        sb.AppendLine($"- **P2 (medium)**: {skills.Count(s => s.Priority == 2)}");
        sb.AppendLine($"- **P3 (low)**: {skills.Count(s => s.Priority == 3)}");

        return sb.ToString();
    }

    // ── Skill Detail ────────────────────────────────────────

    [KernelFunction("skills_detail")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get detailed information about a specific skill's parity status, including migration notes and security requirements.")]
    public static string GetSkillDetail(
        [Description("OpenClaw skill name to look up")] string name)
    {
        var skills = GetCanonicalSkills();
        var skill = skills.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        if (skill is null)
            return $"Unknown skill: `{name}`. Use `skills_parity_matrix` to see all tracked skills.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Skill Detail: {skill.Name}");
        sb.AppendLine();
        sb.AppendLine($"- **Pack**: {skill.Pack}");
        sb.AppendLine($"- **Status**: {GetStatusIcon(skill.Status)} {skill.Status}");
        sb.AppendLine($"- **Priority**: P{skill.Priority}");
        sb.AppendLine($"- **JD.AI Equivalent**: {skill.Equivalent ?? "None yet"}");
        sb.AppendLine($"- **Notes**: {skill.Notes}");

        if (skill.SecurityNotes is not null)
        {
            sb.AppendLine();
            sb.AppendLine("### Security / Governance");
            sb.AppendLine(skill.SecurityNotes);
        }

        if (skill.ImplementationHint is not null)
        {
            sb.AppendLine();
            sb.AppendLine("### Implementation Hint");
            sb.AppendLine(skill.ImplementationHint);
        }

        return sb.ToString();
    }

    // ── Export ───────────────────────────────────────────────

    [KernelFunction("skills_parity_export")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Export the full skills parity data as JSON for CI dashboards and automation.")]
    public static string ExportParity()
    {
        var skills = GetCanonicalSkills();
        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            totalSkills = skills.Count,
            summary = new
            {
                native = skills.Count(s => string.Equals(s.Status, "native", StringComparison.OrdinalIgnoreCase)),
                planned = skills.Count(s => string.Equals(s.Status, "planned", StringComparison.OrdinalIgnoreCase)),
                superseded = skills.Count(s => string.Equals(s.Status, "superseded", StringComparison.OrdinalIgnoreCase)),
                notApplicable = skills.Count(s => string.Equals(s.Status, "not-applicable", StringComparison.OrdinalIgnoreCase))
            },
            skills = skills.Select(s => new
            {
                s.Name,
                s.Pack,
                s.Status,
                s.Priority,
                equivalent = s.Equivalent,
                s.Notes
            }).OrderBy(s => s.Name).ToArray()
        };

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string GetStatusIcon(string status) => status switch
    {
        "native" => "✅",
        "planned" => "📋",
        "superseded" => "🔄",
        "not-applicable" => "➖",
        _ => "❓"
    };

    private static void AppendSummary(StringBuilder sb, List<SkillEntry> skills)
    {
        sb.AppendLine("### Summary");
        var groups = skills.GroupBy(s => s.Status).OrderByDescending(g => g.Count());
        foreach (var g in groups)
        {
            sb.AppendLine($"- **{g.Key}**: {g.Count()}");
        }

        var actionable = skills.Count(s =>
            !string.Equals(s.Status, "not-applicable", StringComparison.OrdinalIgnoreCase));
        var done = skills.Count(s =>
            string.Equals(s.Status, "native", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Status, "superseded", StringComparison.OrdinalIgnoreCase));
        if (actionable > 0)
        {
            sb.AppendLine($"- **Coverage**: {done * 100 / actionable}% ({done}/{actionable} actionable)");
        }
    }

    private static List<SkillEntry> GetCanonicalSkills()
    {
        return
        [
            // ── Productivity & Collaboration ──
            new("trello", "Productivity", "planned", 2, null, "Task board API connector",
                "Requires OAuth token — policy template needed", "Use SK HTTP plugin with Trello REST API"),
            new("notion", "Productivity", "planned", 2, null, "Notion workspace connector",
                "Requires integration token — scoped to workspace", "Notion API v1 with typed page models"),
            new("obsidian", "Productivity", "planned", 2, null, "Obsidian vault file connector",
                null, "Direct file system access to .md vault"),
            new("apple-notes", "Productivity", "not-applicable", 3, null, "macOS only — no Windows/Linux equivalent"),
            new("apple-reminders", "Productivity", "not-applicable", 3, null, "macOS only"),
            new("bear-notes", "Productivity", "not-applicable", 3, null, "macOS only"),
            new("things-mac", "Productivity", "not-applicable", 3, null, "macOS only — use trello/notion instead"),

            // ── Dev / Coding ──
            new("coding-agent", "Dev/Coding", "native", 1, "SubagentTools + TeamOrchestrator",
                "Full multi-agent orchestration with 4 strategies"),
            new("github", "Dev/Coding", "native", 1, "GitHubTools (16 functions)",
                "Comprehensive GitHub via gh CLI"),
            new("gh-issues", "Dev/Coding", "superseded", 1, "GitHubTools.github_list_issues",
                "Folded into unified GitHubTools plugin"),
            new("skill-creator", "Dev/Coding", "planned", 2, null, "Skill scaffolding and authoring tool",
                null, "Generate JDAI.md skill templates with frontmatter"),

            // ── Media / Multimodal ──
            new("nano-pdf", "Media", "native", 1, "MultimodalTools.pdf_analyze",
                "PDF text extraction and analysis"),
            new("video-frames", "Media", "native", 1, "MultimodalTools.media_view",
                "Image/video frame analysis via SK ImageContent"),
            new("nano-banana-pro", "Media", "not-applicable", 3, null, "Proprietary hardware — not in scope"),
            new("openai-image-gen", "Media", "planned", 2, null, "Image generation via SK OpenAI connector",
                "API key required — budget policy applies", "Use DALL-E via Connectors.OpenAI"),
            new("openai-whisper", "Media", "planned", 2, null, "Speech-to-text transcription",
                "API key required", "Use Whisper API via HTTP plugin"),
            new("openai-whisper-api", "Media", "planned", 2, null, "Whisper API integration",
                "Same as openai-whisper", "Combine with openai-whisper"),
            new("sherpa-onnx-tts", "Media", "planned", 3, null, "Local TTS via ONNX runtime",
                null, "Use ONNX Runtime with sherpa models"),
            new("songsee", "Media", "not-applicable", 3, null, "Music recognition — not in scope"),
            new("camsnap", "Media", "planned", 3, null, "Camera capture utility",
                null, "Cross-platform camera access via native interop"),
            new("peekaboo", "Media", "not-applicable", 3, null, "Screen peek — macOS specific"),
            new("gifgrep", "Media", "not-applicable", 3, null, "GIF search — entertainment"),

            // ── Communication ──
            new("discord", "Communication", "native", 1, "JD.AI.Channels.Discord",
                "Full Discord channel adapter with bot support"),
            new("slack", "Communication", "native", 1, "JD.AI.Channels.Slack",
                "Full Slack channel adapter"),
            new("imsg", "Communication", "not-applicable", 3, null, "macOS iMessage — no cross-platform API"),
            new("bluebubbles", "Communication", "not-applicable", 3, null, "macOS iMessage bridge"),
            new("himalaya", "Communication", "planned", 3, null, "Email client integration",
                "Credentials required", "IMAP/SMTP via MailKit"),
            new("voice-call", "Communication", "planned", 3, null, "Voice calling capability",
                "Telephony credentials required", "SIP/WebRTC integration"),

            // ── Device / OS Automation ──
            new("tmux", "Device/OS", "not-applicable", 3, null, "Terminal multiplexer — use shell directly"),
            new("openhue", "Device/OS", "not-applicable", 3, null, "Smart home — not in scope"),
            new("blucli", "Device/OS", "not-applicable", 3, null, "Bluetooth CLI — niche hardware"),
            new("eightctl", "Device/OS", "not-applicable", 3, null, "8BitDo controller — gaming peripheral"),
            new("ordercli", "Device/OS", "not-applicable", 3, null, "Order management — niche"),
            new("wacli", "Device/OS", "not-applicable", 3, null, "WhatsApp CLI — use channels instead"),
            new("goplaces", "Device/OS", "not-applicable", 3, null, "macOS Places — location services"),
            new("sag", "Device/OS", "not-applicable", 3, null, "System agent — macOS specific"),

            // ── Platform / Meta ──
            new("session-logs", "Platform", "native", 1, "SessionOrchestrationTools",
                "Full session management with export/integrity"),
            new("model-usage", "Platform", "native", 1, "UsageTools.get_usage",
                "Token/cost tracking with per-model breakdown"),
            new("healthcheck", "Platform", "native", 1, "GatewayOpsTools.gateway_status",
                "Gateway health and diagnostics"),
            new("summarize", "Platform", "native", 1, "AgentLoop /compact",
                "Conversation summarization built into agent loop"),
            new("mcporter", "Platform", "planned", 1, null, "MCP transport manager",
                "Network access — allowlist required", "Tracked in issue #73"),
            new("clawhub", "Platform", "not-applicable", 3, null, "OpenClaw marketplace — not applicable"),
            new("gemini", "Platform", "native", 1, "SK Connectors.Google",
                "Google Gemini via Semantic Kernel connector"),
            new("canvas", "Platform", "planned", 2, null, "Rich artifact rendering canvas",
                null, "Blazor-based dashboard rendering"),

            // ── Utilities ──
            new("weather", "Utilities", "planned", 3, null, "Weather API integration",
                "API key for weather service", "OpenWeatherMap or similar REST API"),
            new("xurl", "Utilities", "superseded", 1, "WebTools.web_fetch",
                "URL fetching covered by web_fetch tool"),
            new("blogwatcher", "Utilities", "planned", 3, null, "RSS/feed monitoring",
                null, "Use SyndicationFeed for RSS/Atom parsing"),
            new("1password", "Utilities", "planned", 2, null, "1Password secret manager integration",
                "Requires 1Password CLI + service account token — high security", "Use 1Password CLI (op) via shell tool"),
            new("oracle", "Utilities", "not-applicable", 3, null, "Divination — entertainment, not in scope"),
            new("spotify-player", "Utilities", "not-applicable", 3, null, "Music playback — entertainment"),
            new("sonoscli", "Utilities", "not-applicable", 3, null, "Sonos speaker control — entertainment"),
            new("gog", "Utilities", "not-applicable", 3, null, "GOG gaming — entertainment"),
        ];
    }

    internal sealed record SkillEntry(
        string Name,
        string Pack,
        string Status,
        int Priority,
        string? Equivalent,
        string Notes,
        string? SecurityNotes = null,
        string? ImplementationHint = null);
}
