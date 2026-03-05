using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Tools for generating capability documentation, compatibility matrices,
/// migration guides, and governance runbooks from source metadata.
/// </summary>
[ToolPlugin("parityDocs")]
public sealed class ParityDocsTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    // ── Compatibility Matrix ────────────────────────────────

    [KernelFunction("parity_compatibility_matrix")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a versioned compatibility matrix covering JD.AI features across supported agent ecosystems.")]
    public static string GenerateCompatibilityMatrix(
        [Description("Optional category filter: 'tools', 'skills', 'mcp', 'governance', 'runtime', or 'all' (default)")] string? category = null)
    {
        var features = GetFeatureMatrix();

        if (!string.IsNullOrEmpty(category) && !string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
            features = features.Where(f =>
                string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("## JD.AI Capability Matrix");
        sb.AppendLine($"*Generated {DateTime.UtcNow:yyyy-MM-dd}*");
        sb.AppendLine();
        sb.AppendLine("| Feature | Category | JD.AI | OpenClaw | Copilot CLI | Codex CLI |");
        sb.AppendLine("|---------|----------|-------|----------|-------------|-----------|");

        foreach (var f in features.OrderBy(f => f.Category).ThenBy(f => f.Name))
        {
            sb.AppendLine($"| {f.Name} | {f.Category} | {Icon(f.JdAi)} | {Icon(f.OpenClaw)} | {Icon(f.Copilot)} | {Icon(f.Codex)} |");
        }

        sb.AppendLine();
        AppendScoreSummary(sb, features);

        return sb.ToString();
    }

    // ── Migration Guide ─────────────────────────────────────

    [KernelFunction("parity_migration_guide")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a migration guide for users switching from a specific source platform to JD.AI.")]
    public static string GenerateMigrationGuide(
        [Description("Source platform: 'claude', 'openclaw', 'copilot', or 'codex'")] string source)
    {
        var sb = new StringBuilder();
        var sourceName = source.ToLowerInvariant() switch
        {
            "claude" => "Claude Code",
            "openclaw" => "OpenClaw",
            "copilot" => "GitHub Copilot CLI",
            "codex" => "OpenAI Codex CLI",
            _ => source
        };

        sb.AppendLine($"# Migrating from {sourceName} to JD.AI");
        sb.AppendLine();
        sb.AppendLine("## Quick Start");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Install JD.AI");
        sb.AppendLine("dotnet tool install -g jdai");
        sb.AppendLine();
        sb.AppendLine("# Run initial setup");
        sb.AppendLine("jdai");
        sb.AppendLine("```");
        sb.AppendLine();

        var mappings = GetMigrationMappings(source.ToLowerInvariant());

        sb.AppendLine("## Command Mapping");
        sb.AppendLine();
        sb.AppendLine($"| {sourceName} | JD.AI Equivalent | Notes |");
        sb.AppendLine("|---|---|---|");

        foreach (var m in mappings)
        {
            sb.AppendLine($"| `{m.Source}` | `{m.Target}` | {m.Notes} |");
        }

        sb.AppendLine();

        // Config file migration
        sb.AppendLine("## Configuration Migration");
        sb.AppendLine();

        switch (source.ToLowerInvariant())
        {
            case "claude":
                sb.AppendLine("| Claude Code | JD.AI |");
                sb.AppendLine("|---|---|");
                sb.AppendLine("| `CLAUDE.md` | `JDAI.md` (auto-detected, CLAUDE.md also loaded) |");
                sb.AppendLine("| `~/.claude/skills/` | `~/.jdai/skills/` (Claude skills auto-imported) |");
                sb.AppendLine("| `~/.claude/plugins/` | `~/.jdai/plugins/` |");
                sb.AppendLine("| `.claude/settings.json` | `.jdai/config.json` |");
                sb.AppendLine();
                sb.AppendLine("> **Tip**: JD.AI auto-detects and loads CLAUDE.md files. Use `migration_scan` and `migration_convert` tools for automated migration.");
                break;

            case "openclaw":
                sb.AppendLine("| OpenClaw | JD.AI |");
                sb.AppendLine("|---|---|");
                sb.AppendLine("| `openclaw.yaml` | `JDAI.md` + `.jdai/config.json` |");
                sb.AppendLine("| `skills/` directory | `~/.jdai/skills/` |");
                sb.AppendLine("| Channel configs | `JD.AI.Channels.*` NuGet packages |");
                sb.AppendLine();
                sb.AppendLine("> **Tip**: Use `skills_parity_matrix` to see which OpenClaw skills have JD.AI equivalents.");
                break;

            case "copilot":
                sb.AppendLine("| Copilot CLI | JD.AI |");
                sb.AppendLine("|---|---|");
                sb.AppendLine("| `.github/copilot-instructions.md` | `JDAI.md` (auto-detected) |");
                sb.AppendLine("| GitHub auth | GitHub Copilot provider (auto-detected) |");
                sb.AppendLine();
                sb.AppendLine("> **Tip**: JD.AI auto-detects GitHub Copilot authentication and can use it as a provider.");
                break;

            case "codex":
                sb.AppendLine("| Codex CLI | JD.AI |");
                sb.AppendLine("|---|---|");
                sb.AppendLine("| `AGENTS.md` | `JDAI.md` (auto-detected, AGENTS.md also loaded) |");
                sb.AppendLine("| `OPENAI_API_KEY` | Same env var (auto-detected by OpenAI provider) |");
                sb.AppendLine("| Sandbox mode | Configurable sandbox tiers in JDAI.md |");
                break;
        }

        sb.AppendLine();
        sb.AppendLine("## What's Different in JD.AI");
        sb.AppendLine();
        sb.AppendLine("- **Multi-provider**: Use Claude, Copilot, Ollama, Foundry, OpenAI, and more — switch mid-session");
        sb.AppendLine("- **Semantic Kernel native**: Built on Microsoft's AI orchestration framework");
        sb.AppendLine("- **Enterprise governance**: Policy-as-code, RBAC, audit trails, budget controls");
        sb.AppendLine("- **Team orchestration**: 4 strategies (sequential, fan-out, supervisor, debate)");
        sb.AppendLine("- **Gateway mode**: Multi-channel (Discord, Slack, Telegram, Web) via ASP.NET Core");
        sb.AppendLine("- **.NET ecosystem**: Full NuGet package ecosystem, dotnet tool distribution");

        return sb.ToString();
    }

    // ── Governance Runbook ──────────────────────────────────

    [KernelFunction("parity_governance_runbook")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a governance runbook covering security, policy, and operational guidelines for a specific feature area.")]
    public static string GenerateGovernanceRunbook(
        [Description("Feature area: 'tools', 'skills', 'mcp', 'providers', or 'channels'")] string area)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Governance Runbook: {area.ToUpperInvariant()}");
        sb.AppendLine();

        switch (area.ToLowerInvariant())
        {
            case "tools":
                AppendToolsRunbook(sb);
                break;
            case "skills":
                AppendSkillsRunbook(sb);
                break;
            case "mcp":
                AppendMcpRunbook(sb);
                break;
            case "providers":
                AppendProvidersRunbook(sb);
                break;
            case "channels":
                AppendChannelsRunbook(sb);
                break;
            default:
                sb.AppendLine($"Unknown area: `{area}`. Valid: tools, skills, mcp, providers, channels.");
                break;
        }

        return sb.ToString();
    }

    // ── Threat Model ────────────────────────────────────────

    [KernelFunction("parity_threat_model")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a threat model summary for a JD.AI feature, including attack vectors and mitigations.")]
    public static string GenerateThreatModel(
        [Description("Feature to model: 'tool-execution', 'skill-loading', 'mcp-transport', 'provider-auth', 'session-data', or 'gateway'")] string feature)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Threat Model: {feature}");
        sb.AppendLine();

        var threats = GetThreatsForFeature(feature.ToLowerInvariant());
        if (threats.Count == 0)
        {
            sb.AppendLine($"Unknown feature: `{feature}`. Valid: tool-execution, skill-loading, mcp-transport, provider-auth, session-data, gateway.");
            return sb.ToString();
        }

        sb.AppendLine("| # | Threat | Severity | Attack Vector | Mitigation | Status |");
        sb.AppendLine("|---|--------|----------|--------------|------------|--------|");

        var index = 0;
        foreach (var t in threats)
        {
            index++;
            sb.AppendLine($"| {index} | {t.Name} | {t.Severity} | {t.Vector} | {t.Mitigation} | {t.Status} |");
        }

        sb.AppendLine();
        sb.AppendLine("### Risk Summary");
        sb.AppendLine($"- Critical: {threats.Count(t => string.Equals(t.Severity, "🔴 Critical", StringComparison.Ordinal))}");
        sb.AppendLine($"- High: {threats.Count(t => string.Equals(t.Severity, "🟠 High", StringComparison.Ordinal))}");
        sb.AppendLine($"- Medium: {threats.Count(t => string.Equals(t.Severity, "🟡 Medium", StringComparison.Ordinal))}");
        sb.AppendLine($"- Low: {threats.Count(t => string.Equals(t.Severity, "🟢 Low", StringComparison.Ordinal))}");

        return sb.ToString();
    }

    // ── Export ───────────────────────────────────────────────

    [KernelFunction("parity_export")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Export the full parity data (compatibility matrix, migration mappings, governance status) as JSON.")]
    public static string ExportParityData()
    {
        var features = GetFeatureMatrix();
        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            version = "1.0",
            compatibility = features.Select(f => new
            {
                f.Name,
                f.Category,
                jdai = f.JdAi,
                openclaw = f.OpenClaw,
                copilot = f.Copilot,
                codex = f.Codex
            }).ToArray(),
            scores = new
            {
                jdai = features.Count(f => f.JdAi is "full" or "native"),
                openclaw = features.Count(f => f.OpenClaw is "full" or "native"),
                copilot = features.Count(f => f.Copilot is "full" or "native"),
                codex = features.Count(f => f.Codex is "full" or "native"),
                total = features.Count
            }
        };

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    // ── Private helpers ─────────────────────────────────────

    private static string Icon(string status) => status switch
    {
        "full" or "native" => "✅",
        "partial" => "🟡",
        "planned" => "📋",
        "none" => "❌",
        _ => "❓"
    };

    private static void AppendScoreSummary(StringBuilder sb, List<FeatureEntry> features)
    {
        sb.AppendLine("### Coverage Scores");

        int Score(Func<FeatureEntry, string> selector) =>
            features.Count(f => selector(f) is "full" or "native");

        var total = features.Count;
        sb.AppendLine($"- **JD.AI**: {Score(f => f.JdAi)}/{total}");
        sb.AppendLine($"- **OpenClaw**: {Score(f => f.OpenClaw)}/{total}");
        sb.AppendLine($"- **Copilot CLI**: {Score(f => f.Copilot)}/{total}");
        sb.AppendLine($"- **Codex CLI**: {Score(f => f.Codex)}/{total}");
    }

    private static void AppendToolsRunbook(StringBuilder sb)
    {
        sb.AppendLine("## Tool Safety Tiers");
        sb.AppendLine("| Tier | Behavior | Examples |");
        sb.AppendLine("|------|----------|---------|");
        sb.AppendLine("| AutoApprove | No confirmation | read_file, grep, glob, git_status |");
        sb.AppendLine("| ConfirmOnce | Confirm first use per session | write_file, git_commit, git_push |");
        sb.AppendLine("| AlwaysConfirm | Confirm every use | run_command, web_search, execute_code |");
        sb.AppendLine();
        sb.AppendLine("## Operational Checklist");
        sb.AppendLine("- [ ] Review tool whitelist/blacklist in JDAI.md");
        sb.AppendLine("- [ ] Configure `--allowedTools` / `--disallowedTools` for restricted environments");
        sb.AppendLine("- [ ] Enable audit logging for tool invocations");
        sb.AppendLine("- [ ] Set budget limits via `--max-budget-usd`");
        sb.AppendLine("- [ ] Review tool confirmation filter safety tiers");
    }

    private static void AppendSkillsRunbook(StringBuilder sb)
    {
        sb.AppendLine("## Skill Trust Model");
        sb.AppendLine("| Trust Level | Source | Verification |");
        sb.AppendLine("|------------|--------|-------------|");
        sb.AppendLine("| Built-in | Ships with JD.AI | Signed, tested, reviewed |");
        sb.AppendLine("| Verified | NuGet packages | Package signature + hash |");
        sb.AppendLine("| Community | User-installed | Manual review required |");
        sb.AppendLine("| Local | Project JDAI.md | Trusted by project owner |");
        sb.AppendLine();
        sb.AppendLine("## Skill Lifecycle");
        sb.AppendLine("1. **Discovery**: Skills scanned from `~/.jdai/skills/`, `~/.claude/skills/`");
        sb.AppendLine("2. **Validation**: Frontmatter parsed, trust level assigned");
        sb.AppendLine("3. **Loading**: Injected into system prompt (gated by policy)");
        sb.AppendLine("4. **Monitoring**: Usage tracked via audit service");
        sb.AppendLine("5. **Hot-reload**: File watcher detects changes, reloads without restart");
    }

    private static void AppendMcpRunbook(StringBuilder sb)
    {
        sb.AppendLine("## MCP Security Checklist");
        sb.AppendLine("- [ ] All MCP servers use TLS (no plaintext HTTP in production)");
        sb.AppendLine("- [ ] OAuth tokens stored in secure credential store");
        sb.AppendLine("- [ ] Network destinations allowlisted in policy");
        sb.AppendLine("- [ ] MCP tool invocations logged to audit trail");
        sb.AppendLine("- [ ] Rate limiting configured per MCP server");
        sb.AppendLine();
        sb.AppendLine("## Transport Types");
        sb.AppendLine("| Transport | Security | Status |");
        sb.AppendLine("|-----------|----------|--------|");
        sb.AppendLine("| stdio | Process isolation | ✅ Supported |");
        sb.AppendLine("| SSE (HTTP) | TLS required | ✅ Supported |");
        sb.AppendLine("| StreamableHTTP | TLS + auth | 📋 Planned |");
        sb.AppendLine("| WebSocket | TLS + auth | 📋 Planned |");
    }

    private static void AppendProvidersRunbook(StringBuilder sb)
    {
        sb.AppendLine("## Provider Credential Security");
        sb.AppendLine("| Provider | Auth Method | Env Var | Rotation |");
        sb.AppendLine("|----------|------------|---------|----------|");
        sb.AppendLine("| Claude Code | OAuth device flow | Auto | N/A |");
        sb.AppendLine("| GitHub Copilot | GitHub OAuth | Auto | N/A |");
        sb.AppendLine("| OpenAI | API key | OPENAI_API_KEY | 90 days |");
        sb.AppendLine("| Ollama | None (local) | N/A | N/A |");
        sb.AppendLine("| Foundry Local | None (local) | N/A | N/A |");
        sb.AppendLine("| Azure OpenAI | API key + endpoint | AZURE_OPENAI_* | 90 days |");
        sb.AppendLine();
        sb.AppendLine("## Operational Checklist");
        sb.AppendLine("- [ ] Never commit API keys to source control");
        sb.AppendLine("- [ ] Use environment variables or secret managers");
        sb.AppendLine("- [ ] Configure fallback model for provider outages");
        sb.AppendLine("- [ ] Monitor per-provider usage and costs");
    }

    private static void AppendChannelsRunbook(StringBuilder sb)
    {
        sb.AppendLine("## Channel Security");
        sb.AppendLine("| Channel | Auth | DM Pairing | Rate Limit |");
        sb.AppendLine("|---------|------|-----------|------------|");
        sb.AppendLine("| Discord | Bot token | Optional | Per-user |");
        sb.AppendLine("| Slack | Bot OAuth | Optional | Per-workspace |");
        sb.AppendLine("| Telegram | Bot token | Required | Per-chat |");
        sb.AppendLine("| Web | Bearer token | N/A | Per-session |");
        sb.AppendLine("| Signal | signal-cli | Required | Per-number |");
        sb.AppendLine();
        sb.AppendLine("## Operational Checklist");
        sb.AppendLine("- [ ] Rotate bot tokens quarterly");
        sb.AppendLine("- [ ] Enable DM pairing for public-facing channels");
        sb.AppendLine("- [ ] Configure rate limits per channel");
        sb.AppendLine("- [ ] Audit all inbound message sources");
    }

    private static List<ThreatEntry> GetThreatsForFeature(string feature) => feature switch
    {
        "tool-execution" =>
        [
            new("Command injection", "🔴 Critical", "Malicious prompt → run_command with rm -rf", "AlwaysConfirm tier + sandboxing", "✅ Mitigated"),
            new("Path traversal", "🟠 High", "write_file to /etc/passwd via ../", "Working directory lock + path validation", "✅ Mitigated"),
            new("Resource exhaustion", "🟡 Medium", "Infinite loop in run_command", "Timeout enforcement + budget limits", "✅ Mitigated"),
            new("Env var leakage", "🟡 Medium", "run_command exposes secrets via env", "Environment filtering in sandbox", "✅ Mitigated"),
        ],
        "skill-loading" =>
        [
            new("Prompt injection via skill", "🟠 High", "Malicious SKILL.md overrides system prompt", "Trust model + skill content validation", "🟡 Partial"),
            new("Untrusted code in skill", "🟠 High", "Skill references external URLs/scripts", "Content scanning + network allowlist", "📋 Planned"),
            new("Skill shadowing", "🟡 Medium", "Local skill overrides built-in with different behavior", "Precedence logging + admin alerts", "✅ Mitigated"),
        ],
        "mcp-transport" =>
        [
            new("MITM on HTTP transport", "🔴 Critical", "Unencrypted MCP traffic intercepted", "Require TLS for all non-stdio transports", "✅ Mitigated"),
            new("OAuth token theft", "🟠 High", "Credential leakage via logs or memory", "Secure credential store + log redaction", "🟡 Partial"),
            new("Malicious MCP server", "🟠 High", "Compromised server returns harmful tool results", "Server allowlist + result validation", "📋 Planned"),
        ],
        "provider-auth" =>
        [
            new("API key exposure", "🔴 Critical", "Key committed to source or logged", "Env var only + git hooks + log redaction", "✅ Mitigated"),
            new("Token replay", "🟡 Medium", "Stolen OAuth token reused", "Short-lived tokens + refresh rotation", "✅ Mitigated"),
            new("Provider impersonation", "🟢 Low", "Fake provider endpoint", "Certificate pinning for known providers", "📋 Planned"),
        ],
        "session-data" =>
        [
            new("Session data exfiltration", "🟠 High", "Sensitive code/secrets in session history", "Local-only storage + encryption at rest", "🟡 Partial"),
            new("Session tampering", "🟡 Medium", "Modified session replay", "Session integrity hash validation", "✅ Mitigated"),
            new("Cross-session leakage", "🟢 Low", "Data from one session visible in another", "Session isolation + per-session DB", "✅ Mitigated"),
        ],
        "gateway" =>
        [
            new("Unauthenticated API access", "🔴 Critical", "Public gateway endpoint exposed", "API key + Bearer token middleware", "✅ Mitigated"),
            new("DDoS via channel flood", "🟠 High", "Message flood from public channel", "Rate limiting per channel/user", "✅ Mitigated"),
            new("Privilege escalation", "🟠 High", "User gains admin role", "RBAC with least-privilege defaults", "✅ Mitigated"),
            new("SignalR injection", "🟡 Medium", "Malicious payloads via WebSocket", "Input validation + message size limits", "✅ Mitigated"),
        ],
        _ => []
    };

    private static List<CommandMapping> GetMigrationMappings(string source) => source switch
    {
        "claude" =>
        [
            new("claude", "jdai", "Equivalent command"),
            new("claude -p \"query\"", "jdai -p \"query\"", "Print mode"),
            new("claude --continue", "jdai --continue", "Resume session"),
            new("claude --model", "jdai --model", "Model selection"),
            new("/compact", "/compact", "Same command"),
            new("/model", "/model", "Same command"),
            new("/help", "/help", "Same command"),
            new("/clear", "/clear", "Same command"),
            new("CLAUDE.md", "JDAI.md", "Auto-detected (both work)"),
        ],
        "openclaw" =>
        [
            new("openclaw", "jdai", "Equivalent command"),
            new("openclaw chat", "jdai", "TUI is default mode"),
            new("openclaw gateway", "jdai gateway", "Gateway mode"),
            new("openclaw skills list", "skills_parity_matrix tool", "Via agent tools"),
            new("openclaw channels", "jdai gateway --channels", "Via gateway"),
        ],
        "copilot" =>
        [
            new("gh copilot suggest", "jdai -p", "Print mode"),
            new("gh copilot explain", "jdai -p \"explain...\"", "Print mode with prompt"),
            new("copilot-instructions.md", "JDAI.md", "Auto-detected (both work)"),
        ],
        "codex" =>
        [
            new("codex", "jdai", "Equivalent command"),
            new("codex --model", "jdai --model", "Model selection"),
            new("codex --approval-mode", "jdai --permission-mode", "Similar concept"),
            new("AGENTS.md", "JDAI.md", "Auto-detected (both work)"),
        ],
        _ => []
    };

    private static List<FeatureEntry> GetFeatureMatrix()
    {
        return
        [
            // Tools
            new("File read/write/edit", "tools", "full", "full", "full", "full"),
            new("Shell execution", "tools", "full", "full", "partial", "full"),
            new("Git operations", "tools", "full", "full", "partial", "full"),
            new("Web fetch", "tools", "full", "full", "partial", "none"),
            new("Web search", "tools", "full", "partial", "partial", "none"),
            new("Code search (grep/glob)", "tools", "full", "full", "full", "full"),
            new("Memory/embeddings", "tools", "full", "full", "none", "none"),
            new("Notebook execution", "tools", "full", "partial", "none", "none"),
            new("Clipboard access", "tools", "full", "none", "none", "none"),
            new("Batch file editing", "tools", "full", "none", "none", "partial"),
            new("Multimodal (image/PDF)", "tools", "full", "partial", "partial", "none"),
            new("Browser automation", "tools", "full", "partial", "none", "none"),

            // Skills
            new("Project instructions (CLAUDE.md/JDAI.md)", "skills", "full", "full", "full", "full"),
            new("Skill loading (directory)", "skills", "full", "full", "none", "none"),
            new("Plugin system", "skills", "full", "full", "none", "none"),
            new("Skill hot-reload", "skills", "full", "full", "none", "none"),
            new("Skill trust/gating", "skills", "full", "partial", "none", "none"),

            // MCP
            new("MCP stdio transport", "mcp", "full", "full", "none", "none"),
            new("MCP SSE transport", "mcp", "full", "full", "none", "none"),
            new("MCP StreamableHTTP", "mcp", "planned", "planned", "none", "none"),
            new("MCP OAuth", "mcp", "planned", "planned", "none", "none"),

            // Governance
            new("Tool safety tiers", "governance", "full", "partial", "none", "partial"),
            new("Policy-as-code", "governance", "full", "partial", "none", "none"),
            new("RBAC", "governance", "full", "partial", "none", "none"),
            new("Audit logging", "governance", "full", "partial", "none", "none"),
            new("Budget controls", "governance", "full", "none", "none", "none"),
            new("Signed capabilities", "governance", "planned", "planned", "none", "none"),

            // Runtime
            new("Multi-provider", "runtime", "full", "none", "none", "none"),
            new("Subagents", "runtime", "full", "full", "full", "partial"),
            new("Team orchestration", "runtime", "full", "partial", "none", "none"),
            new("Session persistence", "runtime", "full", "full", "partial", "none"),
            new("Git checkpointing", "runtime", "full", "partial", "none", "none"),
            new("Gateway/multi-channel", "runtime", "full", "full", "none", "none"),
            new("Daemon mode", "runtime", "full", "full", "none", "none"),
            new("Dashboard UI", "runtime", "full", "partial", "none", "none"),
        ];
    }

    private sealed record FeatureEntry(string Name, string Category, string JdAi, string OpenClaw, string Copilot, string Codex);
    private sealed record CommandMapping(string Source, string Target, string Notes);
    private sealed record ThreatEntry(string Name, string Severity, string Vector, string Mitigation, string Status);
}
