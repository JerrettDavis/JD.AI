using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Migration tools for importing Claude Code skills, plugins, and hooks
/// into JD.AI-native capabilities.
/// </summary>
[ToolPlugin("migration")]
public sealed class MigrationTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    // ── Scan ────────────────────────────────────────────────

    [KernelFunction("migration_scan")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Scan a Claude Code installation directory for skills, plugins, and hooks that can be migrated to JD.AI. Defaults to ~/.claude.")]
    public static string ScanClaudeInstallation(
        [Description("Path to Claude Code config directory (default: ~/.claude)")] string? claudePath = null)
    {
        claudePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        if (!Directory.Exists(claudePath))
            return $"Claude directory not found at: {claudePath}";

        var sb = new StringBuilder();
        sb.AppendLine("## Claude Code Migration Scan");
        sb.AppendLine($"**Source**: {claudePath}");
        sb.AppendLine();

        // Scan skills
        var skillsDir = Path.Combine(claudePath, "skills");
        var skills = ScanSkills(skillsDir);
        sb.AppendLine($"### Skills ({skills.Count})");
        if (skills.Count > 0)
        {
            foreach (var skill in skills.OrderBy(s => s.Name))
            {
                sb.AppendLine($"- `{skill.Name}`: {skill.Description ?? "(no description)"}");
            }
        }
        else
        {
            sb.AppendLine("  No skills found.");
        }
        sb.AppendLine();

        // Scan plugins
        var pluginsDir = Path.Combine(claudePath, "plugins");
        var plugins = ScanPlugins(pluginsDir);
        sb.AppendLine($"### Plugins ({plugins.Count})");
        if (plugins.Count > 0)
        {
            foreach (var plugin in plugins.OrderBy(p => p.Name))
            {
                sb.AppendLine($"- `{plugin.Name}`: {plugin.Description ?? "(no description)"}");
            }
        }
        else
        {
            sb.AppendLine("  No plugins found.");
        }
        sb.AppendLine();

        // Scan CLAUDE.md
        var claudeMd = Path.Combine(claudePath, "CLAUDE.md");
        var hasClaude = File.Exists(claudeMd);
        sb.AppendLine("### Project Instructions");
        sb.AppendLine(
            hasClaude ? $"  ✓ CLAUDE.md found ({new FileInfo(claudeMd).Length} bytes)" : "  ✗ No CLAUDE.md");
        sb.AppendLine();

        // Summary
        sb.AppendLine("### Migration Summary");
        sb.AppendLine($"- **Skills**: {skills.Count} found");
        sb.AppendLine($"- **Plugins**: {plugins.Count} found");
        sb.AppendLine($"- **Instructions**: {(hasClaude ? "Yes" : "No")}");
        sb.AppendLine();
        sb.AppendLine("Use `migration_analyze` to get detailed migration recommendations.");

        return sb.ToString();
    }

    // ── Analyze ─────────────────────────────────────────────

    [KernelFunction("migration_analyze")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Analyze a specific Claude skill or plugin and generate migration recommendations for JD.AI.")]
    public static string AnalyzeSkill(
        [Description("Name of the skill/plugin to analyze")] string name,
        [Description("Path to Claude config directory (default: ~/.claude)")] string? claudePath = null)
    {
        claudePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        var sb = new StringBuilder();
        sb.AppendLine($"## Migration Analysis: {name}");
        sb.AppendLine();

        // Try to find the skill
        var skillDir = Path.Combine(claudePath, "skills", name);
        var pluginDir = Path.Combine(claudePath, "plugins", name);
        var installedPluginDir = Path.Combine(claudePath, "installed-plugins");

        string? skillPath = null;
        string? content = null;

        if (Directory.Exists(skillDir))
        {
            skillPath = Path.Combine(skillDir, "SKILL.md");
            if (File.Exists(skillPath))
                content = File.ReadAllText(skillPath);
        }
        else if (Directory.Exists(pluginDir))
        {
            skillPath = Directory.GetFiles(pluginDir, "*.md").FirstOrDefault();
            if (skillPath is not null)
                content = File.ReadAllText(skillPath);
        }
        else if (Directory.Exists(installedPluginDir))
        {
            // Search recursively in installed-plugins
            var dirs = Directory.GetDirectories(installedPluginDir, name, SearchOption.AllDirectories);
            if (dirs.Length > 0)
            {
                skillPath = Directory.GetFiles(dirs[0], "SKILL.md", SearchOption.AllDirectories)
                    .FirstOrDefault()
                    ?? Directory.GetFiles(dirs[0], "*.md", SearchOption.AllDirectories)
                        .FirstOrDefault();
                if (skillPath is not null)
                    content = File.ReadAllText(skillPath);
            }
        }

        if (content is null)
        {
            sb.AppendLine($"Could not find skill/plugin '{name}' in {claudePath}");
            return sb.ToString();
        }

        sb.AppendLine($"**Source**: {skillPath}");
        sb.AppendLine($"**Size**: {content.Length} chars, {content.Split('\n').Length} lines");
        sb.AppendLine();

        // Parse frontmatter
        var (frontmatter, body) = ParseFrontmatter(content);

        if (frontmatter.Count > 0)
        {
            sb.AppendLine("### Metadata");
            foreach (var (key, value) in frontmatter)
            {
                sb.AppendLine($"- **{key}**: {value}");
            }
            sb.AppendLine();
        }

        // Analyze content for migration recommendations
        sb.AppendLine("### Migration Recommendations");

        // Check if it's a prompt-based skill (most Claude skills are)
        if (body.Contains("## ", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("### ", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("- ✅ **Type**: Prompt-based skill — can be loaded as JDAI.md instructions");
            sb.AppendLine("- 📋 **Action**: Copy skill content to `.jdai/skills/{name}/instructions.md`");
        }

        // Check for tool references
        if (body.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
            (body.Contains("bash", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("shell", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("command", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine("- ⚠️ **Has tool usage**: References shell/command tools — verify tool names match JD.AI");
        }

        // Check for MCP references
        if (body.Contains("mcp", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("- 🔌 **MCP integration**: References MCP servers — check JD.AI MCP config compatibility");
        }

        // Check for Claude-specific features
        if (body.Contains("claude", StringComparison.OrdinalIgnoreCase) &&
            (body.Contains("artifact", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("thinking", StringComparison.OrdinalIgnoreCase)))
        {
            sb.AppendLine("- ⚠️ **Claude-specific**: References Claude-specific features (artifacts/thinking) — may need adaptation");
        }

        // Known skill mappings
        var knownMappings = GetKnownMappings();
        if (knownMappings.TryGetValue(name.ToLowerInvariant(), out var mapping))
        {
            sb.AppendLine();
            sb.AppendLine("### JD.AI Equivalent");
            sb.AppendLine($"- **Status**: {mapping.Status}");
            sb.AppendLine($"- **Notes**: {mapping.Notes}");
        }

        sb.AppendLine();
        sb.AppendLine("### Content Preview");
        var preview = body.Length > 500 ? string.Concat(body.AsSpan(0, 497), "...") : body;
        sb.AppendLine("```markdown");
        sb.AppendLine(preview);
        sb.AppendLine("```");

        return sb.ToString();
    }

    // ── Convert ─────────────────────────────────────────────

    [KernelFunction("migration_convert")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Convert a Claude CLAUDE.md file to JD.AI JDAI.md format.")]
    public static string ConvertInstructions(
        [Description("Path to CLAUDE.md file, or content directly")] string input)
    {
        string content;
        if (File.Exists(input))
        {
            content = File.ReadAllText(input);
        }
        else
        {
            content = input;
        }

        var sb = new StringBuilder();
        sb.AppendLine("# JD.AI Project Instructions");
        sb.AppendLine("# (Converted from CLAUDE.md)");
        sb.AppendLine();

        // Replace Claude-specific references
        var converted = content
            .Replace("Claude Code", "JD.AI", StringComparison.OrdinalIgnoreCase)
            .Replace("claude code", "JD.AI", StringComparison.OrdinalIgnoreCase)
            .Replace("CLAUDE.md", "JDAI.md", StringComparison.OrdinalIgnoreCase);

        sb.Append(converted);

        return sb.ToString();
    }

    // ── Parity Matrix ───────────────────────────────────────

    [KernelFunction("migration_parity")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a parity matrix showing Claude skill equivalents in JD.AI. Shows what's native, planned, or not applicable.")]
    public static string GenerateParityMatrix()
    {
        var mappings = GetKnownMappings();
        var sb = new StringBuilder();
        sb.AppendLine("## Claude → JD.AI Skill Parity Matrix");
        sb.AppendLine();
        sb.AppendLine("| Claude Skill | JD.AI Status | JD.AI Equivalent | Notes |");
        sb.AppendLine("|-------------|-------------|------------------|-------|");

        foreach (var (name, mapping) in mappings.OrderBy(m => m.Key))
        {
            var icon = mapping.Status switch
            {
                "native" => "✅",
                "partial" => "🟡",
                "planned" => "📋",
                "superseded" => "🔄",
                "not-applicable" => "➖",
                _ => "❓"
            };

            sb.AppendLine($"| `{name}` | {icon} {mapping.Status} | {mapping.Equivalent ?? "-"} | {mapping.Notes} |");
        }

        sb.AppendLine();

        var stats = mappings.GroupBy(m => m.Value.Status)
            .OrderByDescending(g => g.Count());
        sb.AppendLine("### Summary");
        foreach (var group in stats)
        {
            sb.AppendLine($"- **{group.Key}**: {group.Count()}");
        }

        return sb.ToString();
    }

    // ── Export ───────────────────────────────────────────────

    [KernelFunction("migration_export")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Export migration scan results as JSON for CI/automation integration.")]
    public static string ExportScanResults(
        [Description("Path to Claude config directory (default: ~/.claude)")] string? claudePath = null)
    {
        claudePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        var skills = ScanSkills(Path.Combine(claudePath, "skills"));
        var plugins = ScanPlugins(Path.Combine(claudePath, "plugins"));

        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            source = claudePath,
            skills = skills.Select(s => new { s.Name, s.Description, s.Path }).ToArray(),
            plugins = plugins.Select(p => new { p.Name, p.Description, p.Path }).ToArray(),
            mappings = GetKnownMappings().Select(m => new
            {
                claudeSkill = m.Key,
                m.Value.Status,
                m.Value.Equivalent,
                m.Value.Notes
            }).ToArray()
        };

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static List<DiscoveredItem> ScanSkills(string skillsDir)
    {
        var items = new List<DiscoveredItem>();
        if (!Directory.Exists(skillsDir)) return items;

        foreach (var dir in Directory.GetDirectories(skillsDir))
        {
            var name = Path.GetFileName(dir);
            var skillMd = Path.Combine(dir, "SKILL.md");
            string? description = null;

            if (File.Exists(skillMd))
            {
                var content = File.ReadAllText(skillMd);
                var (fm, _) = ParseFrontmatter(content);
                fm.TryGetValue("description", out description);
            }

            items.Add(new DiscoveredItem(name, description, dir));
        }

        return items;
    }

    private static List<DiscoveredItem> ScanPlugins(string pluginsDir)
    {
        var items = new List<DiscoveredItem>();
        if (!Directory.Exists(pluginsDir)) return items;

        foreach (var dir in Directory.GetDirectories(pluginsDir))
        {
            var name = Path.GetFileName(dir);
            string? description = null;

            // Try to read README or plugin.json
            var readmePath = Path.Combine(dir, "README.md");
            if (File.Exists(readmePath))
            {
                var lines = File.ReadAllLines(readmePath);
                description = lines.Length > 1 ? lines[1].TrimStart('#', ' ') : null;
            }

            items.Add(new DiscoveredItem(name, description, dir));
        }

        return items;
    }

    private static (Dictionary<string, string> Frontmatter, string Body) ParseFrontmatter(string content)
    {
        var fm = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!content.StartsWith("---", StringComparison.Ordinal))
            return (fm, content);

        var endIdx = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIdx < 0)
            return (fm, content);

        var fmBlock = content[3..endIdx].Trim();
        var body = content[(endIdx + 3)..].TrimStart();

        foreach (var line in fmBlock.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIdx = line.IndexOf(':', StringComparison.Ordinal);
            if (colonIdx <= 0) continue;

            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim().Trim('"');
            fm[key] = value;
        }

        return (fm, body);
    }

    private static Dictionary<string, SkillMapping> GetKnownMappings()
    {
        return new Dictionary<string, SkillMapping>(StringComparer.OrdinalIgnoreCase)
        {
            // Productivity & Collaboration
            ["brainstorming"] = new("native", "JDAI.md instructions", "Loaded via skills loader"),
            ["commiting-code"] = new("native", "JDAI.md instructions", "Git commit guidelines loaded as project instructions"),
            ["git-commit-guidelines"] = new("native", "JDAI.md instructions", "Same as committing-code"),
            ["skill-writer"] = new("planned", null, "JD.AI skill scaffolding tool planned"),
            ["docfx-documentation"] = new("native", "JDAI.md instructions", "DocFX skill loaded as instructions"),
            ["systematic-debugging"] = new("native", "JDAI.md instructions", "Debugging skill loaded as instructions"),
            ["architecture-patterns"] = new("native", "JDAI.md instructions", "Architecture skill loaded as instructions"),
            ["frontend-design"] = new("native", "JDAI.md instructions", "Frontend design skill loaded as instructions"),

            // Dev/Coding
            ["coding-agent"] = new("native", "SubagentTools + orchestration", "Full multi-agent orchestration system"),
            ["github"] = new("native", "GitHubTools", "16 native GitHub tools via gh CLI"),
            ["gh-issues"] = new("superseded", "GitHubTools.github_list_issues", "Covered by GitHubTools"),
            ["trello"] = new("planned", null, "Task board integration planned"),
            ["notion"] = new("planned", null, "Notion API connector planned"),
            ["obsidian"] = new("planned", null, "Obsidian vault connector planned"),

            // Media/Multimodal
            ["openai-image-gen"] = new("planned", null, "Image generation via SK connector"),
            ["openai-whisper"] = new("planned", null, "Speech-to-text planned"),
            ["openai-whisper-api"] = new("planned", null, "Speech-to-text API planned"),
            ["video-frames"] = new("partial", "MultimodalTools.media_view", "Basic media analysis available"),
            ["nano-pdf"] = new("native", "MultimodalTools.pdf_analyze", "Native PDF analysis tool"),

            // Communication
            ["discord"] = new("native", "JD.AI.Channels.Discord", "Full Discord channel adapter"),
            ["slack"] = new("native", "JD.AI.Channels.Slack", "Full Slack channel adapter"),
            ["imsg"] = new("not-applicable", null, "macOS only — not applicable"),
            ["bluebubbles"] = new("not-applicable", null, "macOS iMessage bridge — not applicable"),

            // Device/OS
            ["apple-notes"] = new("not-applicable", null, "macOS only"),
            ["apple-reminders"] = new("not-applicable", null, "macOS only"),
            ["things-mac"] = new("not-applicable", null, "macOS only"),
            ["bear-notes"] = new("not-applicable", null, "macOS only"),
            ["tmux"] = new("not-applicable", null, "Terminal multiplexer — use directly via shell"),

            // Utilities
            ["weather"] = new("planned", null, "Weather API tool planned"),
            ["xurl"] = new("superseded", "WebTools.web_fetch", "Covered by web_fetch tool"),
            ["blogwatcher"] = new("planned", null, "RSS/feed monitoring planned"),
            ["summarize"] = new("native", "AgentLoop built-in", "Summarization via /compact and agent prompts"),
            ["session-logs"] = new("native", "SessionOrchestrationTools", "Full session management tools"),
            ["model-usage"] = new("native", "UsageTools.get_usage", "Token/cost tracking built-in"),
            ["healthcheck"] = new("native", "GatewayOpsTools.gateway_status", "Gateway health check tool"),

            // Platform
            ["skill-creator"] = new("planned", null, "Skill scaffolding tool planned"),
            ["mcporter"] = new("planned", null, "MCP transport manager planned"),
            ["clawhub"] = new("not-applicable", null, "OpenClaw marketplace — not applicable"),
            ["gemini"] = new("native", "SK Connector", "Google Gemini via Semantic Kernel connector"),
            ["spotify-player"] = new("not-applicable", null, "Entertainment — not in scope"),
            ["sonoscli"] = new("not-applicable", null, "Entertainment — not in scope"),
            ["1password"] = new("planned", null, "Secret manager integration planned"),
            ["openhue"] = new("not-applicable", null, "Smart home — not in scope"),
            ["gog"] = new("not-applicable", null, "Gaming — not in scope"),
            ["oracle"] = new("not-applicable", null, "Divination — not in scope"),
        };
    }

    private sealed record DiscoveredItem(string Name, string? Description, string Path);
    private sealed record SkillMapping(string Status, string? Equivalent, string Notes);
}
