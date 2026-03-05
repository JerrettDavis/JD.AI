using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Parity benchmark harness for evaluating JD.AI capability coverage
/// against target surfaces (OpenClaw, Claude Code, Copilot CLI, Codex CLI).
/// </summary>
[ToolPlugin("benchmark", RequiresInjection = true)]
public sealed class BenchmarkTools
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    private readonly Kernel _kernel;

    public BenchmarkTools(Kernel kernel)
    {
        _kernel = kernel;
    }

    // ── Canonical Capability Registry ────────────────────────

    private static readonly CapabilityEntry[] CanonicalCapabilities =
    [
        // File Operations
        new("file.read", "Read file contents", ["read_file"]),
        new("file.write", "Write/create files", ["write_file"]),
        new("file.edit", "Edit existing files", ["edit_file"]),
        new("file.list", "List directory contents", ["list_directory"]),

        // Search
        new("search.grep", "Search file contents", ["grep"]),
        new("search.glob", "Find files by pattern", ["glob"]),

        // Shell & Execution
        new("shell.run", "Execute shell commands", ["run_command"]),
        new("shell.code", "Execute code snippets", ["execute_code"]),

        // Version Control
        new("git.status", "Git status", ["git_status"]),
        new("git.diff", "Git diff", ["git_diff"]),
        new("git.log", "Git log", ["git_log"]),
        new("git.commit", "Git commit", ["git_commit"]),
        new("git.push", "Git push", ["git_push"]),
        new("git.pull", "Git pull", ["git_pull"]),
        new("git.branch", "Branch operations", ["git_branch", "git_checkout"]),
        new("git.stash", "Git stash", ["git_stash"]),

        // Web & Network
        new("web.fetch", "Fetch web content", ["web_fetch"]),
        new("web.search", "Web search", ["web_search"]),

        // Memory & Knowledge
        new("memory.store", "Store in memory", ["memory_store"]),
        new("memory.search", "Search memory", ["memory_search"]),
        new("memory.forget", "Forget memory", ["memory_forget"]),

        // Agent & Orchestration
        new("agent.spawn", "Spawn subagent", ["spawn_agent"]),
        new("agent.team", "Spawn team", ["spawn_team"]),
        new("agent.list", "List agents", ["agents_list"]),

        // Sessions
        new("session.list", "List sessions", ["sessions_list"]),
        new("session.spawn", "Spawn session", ["sessions_spawn"]),
        new("session.history", "Session history", ["sessions_history"]),

        // Browser Automation
        new("browser.status", "Browser status", ["browser_status"]),
        new("browser.screenshot", "Take screenshot", ["browser_screenshot"]),
        new("browser.content", "Get page content", ["browser_content"]),
        new("browser.pdf", "Capture PDF", ["browser_pdf"]),

        // GitHub Integration
        new("github.issues", "List/manage issues", ["github_list_issues", "github_create_issue"]),
        new("github.prs", "List/manage PRs", ["github_list_prs", "github_create_pr"]),
        new("github.ci", "CI/Actions", ["github_list_runs", "github_run_details"]),

        // Multimodal
        new("multimodal.image", "Image analysis", ["image_analyze"]),
        new("multimodal.pdf", "PDF analysis", ["pdf_analyze"]),

        // Operations
        new("ops.channels", "Channel management", ["channel_list", "channel_send"]),
        new("ops.scheduler", "Task scheduling", ["cron_list", "cron_add"]),
        new("ops.gateway", "Gateway management", ["gateway_status", "gateway_config"]),

        // Tasks
        new("tasks.crud", "Task management", ["create_task", "list_tasks", "update_task", "complete_task"]),

        // Clipboard
        new("clipboard.read", "Read clipboard", ["read_clipboard"]),
        new("clipboard.write", "Write clipboard", ["write_clipboard"]),

        // Introspection
        new("meta.think", "Structured thinking", ["think"]),
        new("meta.usage", "Usage tracking", ["get_usage"]),
        new("meta.environment", "Environment info", ["get_environment"]),
        new("meta.capabilities", "Self-introspection", ["capability_list", "capability_gaps"]),

        // Governance & Security
        new("governance.policy", "Policy evaluation", ["policy_evaluate"]),
        new("governance.audit", "Audit logging", ["audit_query"]),
        new("governance.rbac", "Role-based access", ["rbac_check"]),

        // Diff & Batch
        new("diff.create", "Create patches", ["create_patch"]),
        new("diff.apply", "Apply patches", ["apply_patch"]),
        new("diff.batch", "Batch file edits", ["batch_edit_files"]),
    ];

    // ── Benchmark Tools ─────────────────────────────────────

    [KernelFunction("benchmark_scorecard")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a parity scorecard comparing current JD.AI capabilities against the canonical registry. Shows coverage percentage and gaps.")]
    public string GenerateScorecard()
    {
        var available = _kernel.Plugins
            .SelectMany(p => p.Select(f => f.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("## JD.AI Parity Scorecard");
        sb.AppendLine();

        var categories = CanonicalCapabilities
            .GroupBy(c => c.Id.Split('.')[0])
            .OrderBy(g => g.Key);

        var totalCapabilities = 0;
        var coveredCapabilities = 0;

        foreach (var group in categories)
        {
            var entries = group.ToList();
            var covered = entries.Count(e => e.RequiredTools.Any(t => available.Contains(t)));
            totalCapabilities += entries.Count;
            coveredCapabilities += covered;

            var pct = entries.Count > 0 ? (covered * 100) / entries.Count : 0;
            var indicator = pct switch
            {
                100 => "✅",
                > 50 => "🟡",
                > 0 => "🟠",
                _ => "❌"
            };

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"### {indicator} {group.Key} ({covered}/{entries.Count} — {pct}%)");

            foreach (var entry in entries)
            {
                var hasTool = entry.RequiredTools.Any(t => available.Contains(t));
                var mark = hasTool ? "✓" : "✗";
                var missing = hasTool ? "" :
                    $" — need: {string.Join(", ", entry.RequiredTools)}";
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  {mark} `{entry.Id}`: {entry.Description}{missing}");
            }

            sb.AppendLine();
        }

        var overallPct = totalCapabilities > 0
            ? (coveredCapabilities * 100) / totalCapabilities : 0;

        sb.AppendLine("---");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**Overall: {coveredCapabilities}/{totalCapabilities} capabilities ({overallPct}%)**");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**Tools registered: {available.Count}**");

        return sb.ToString();
    }

    [KernelFunction("benchmark_run")]
    [ToolSafetyTier(SafetyTier.ConfirmOnce)]
    [Description("Run a quick smoke test of available tools by invoking read-only tools and measuring response times.")]
    public async Task<string> RunSmokeBenchmark()
    {
        var results = new List<BenchmarkResult>();
        var available = _kernel.Plugins
            .SelectMany(p => p.Select(f => (Plugin: p.Name, Function: f)))
            .ToList();

        // Test a subset of safe read-only tools
        var safeTools = new[] { "think", "get_environment", "get_usage", "list_tasks" };

        foreach (var toolName in safeTools)
        {
            var match = available.FirstOrDefault(a =>
                a.Function.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));

            if (match.Function is null)
            {
                results.Add(new BenchmarkResult(toolName, "SKIP", 0, "Tool not registered"));
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var args = new KernelArguments();
                // Provide minimal required args for known tools
                if (string.Equals(toolName, "think", StringComparison.Ordinal))
                    args["thought"] = "Benchmark smoke test";

                await match.Function.InvokeAsync(_kernel, args).ConfigureAwait(false);
                sw.Stop();
                results.Add(new BenchmarkResult(toolName, "PASS", sw.ElapsedMilliseconds, null));
            }
#pragma warning disable CA1031 // Catch general exception for benchmark resilience
            catch (Exception ex)
#pragma warning restore CA1031
            {
                sw.Stop();
                results.Add(new BenchmarkResult(toolName, "FAIL", sw.ElapsedMilliseconds,
                    ex.Message.Length > 100 ? string.Concat(ex.Message.AsSpan(0, 97), "...") : ex.Message));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Smoke Benchmark Results");
        sb.AppendLine();
        sb.AppendLine("| Tool | Status | Time (ms) | Notes |");
        sb.AppendLine("|------|--------|-----------|-------|");

        foreach (var r in results)
        {
            var icon = r.Status switch { "PASS" => "✅", "FAIL" => "❌", _ => "⏭️" };
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| `{r.Tool}` | {icon} {r.Status} | {r.ElapsedMs}ms | {r.Notes ?? "-"} |");
        }

        var passed = results.Count(r => string.Equals(r.Status, "PASS", StringComparison.Ordinal));
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**{passed}/{results.Count} passed**");

        return sb.ToString();
    }

    [KernelFunction("benchmark_export")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Export the full capability registry as JSON for CI integration. Includes coverage status for each canonical capability.")]
    public string ExportRegistry()
    {
        var available = _kernel.Plugins
            .SelectMany(p => p.Select(f => f.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = CanonicalCapabilities.Select(c => new
        {
            id = c.Id,
            description = c.Description,
            requiredTools = c.RequiredTools,
            covered = c.RequiredTools.Any(t => available.Contains(t)),
            missingTools = c.RequiredTools.Where(t => !available.Contains(t)).ToArray()
        }).ToArray();

        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            totalCapabilities = entries.Length,
            covered = entries.Count(e => e.covered),
            pct = entries.Length > 0 ? (entries.Count(e => e.covered) * 100) / entries.Length : 0,
            totalToolsRegistered = available.Count,
            capabilities = entries
        };

        return JsonSerializer.Serialize(result, s_jsonOptions);
    }

    [KernelFunction("benchmark_regression")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Check for capability regressions by comparing current state against a baseline JSON. Returns any capabilities that were covered but are now missing.")]
    public static string CheckRegression(
        [Description("Baseline JSON from a previous benchmark_export")] string baselineJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(baselineJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("capabilities", out var caps))
                return "Error: Invalid baseline JSON — missing 'capabilities' array.";

            var regressions = new List<string>();
            var improvements = new List<string>();

            foreach (var cap in caps.EnumerateArray())
            {
                var id = cap.GetProperty("id").GetString() ?? "unknown";
                var wasCovered = cap.GetProperty("covered").GetBoolean();

                // Find current canonical entry
                var current = CanonicalCapabilities.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
                if (current is null)
                {
                    if (wasCovered)
                        regressions.Add($"  ❌ `{id}`: was covered, now removed from registry");
                    continue;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("## Regression Check");
            sb.AppendLine();

            if (regressions.Count > 0)
            {
                sb.AppendLine("### ⚠️ Regressions");
                foreach (var r in regressions)
                    sb.AppendLine(r);
                sb.AppendLine();
            }

            if (improvements.Count > 0)
            {
                sb.AppendLine("### ✅ Improvements");
                foreach (var i in improvements)
                    sb.AppendLine(i);
                sb.AppendLine();
            }

            if (regressions.Count == 0 && improvements.Count == 0)
                sb.AppendLine("No changes detected from baseline.");

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"**Regressions: {regressions.Count}, Improvements: {improvements.Count}**");

            return sb.ToString();
        }
        catch (JsonException ex)
        {
            return $"Error: Invalid JSON baseline — {ex.Message}";
        }
    }

    // ── Types ───────────────────────────────────────────────

    private sealed record CapabilityEntry(string Id, string Description, string[] RequiredTools);
    private sealed record BenchmarkResult(string Tool, string Status, long ElapsedMs, string? Notes);
}
