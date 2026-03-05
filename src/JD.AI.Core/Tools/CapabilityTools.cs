using System.ComponentModel;
using System.Globalization;
using System.Text;
using JD.AI.Core.Attributes;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Self-introspection and capability discovery tools.
/// Enables the agent to understand its own tools, analyze usage, and suggest improvements.
/// </summary>
[ToolPlugin("capabilities", RequiresInjection = true)]
public sealed class CapabilityTools
{
    private readonly Kernel _kernel;
    private readonly Dictionary<string, int> _toolUsage = new(StringComparer.OrdinalIgnoreCase);

    public CapabilityTools(Kernel kernel)
    {
        _kernel = kernel;
    }

    /// <summary>Records a tool invocation for usage analysis.</summary>
    public void RecordUsage(string toolName)
    {
        lock (_toolUsage)
        {
            _toolUsage.TryGetValue(toolName, out var count);
            _toolUsage[toolName] = count + 1;
        }
    }

    // ── Discovery ───────────────────────────────────────────

    [KernelFunction("capability_list")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("List all registered tools grouped by plugin with descriptions. Use this to understand what tools are available.")]
    public string ListCapabilities(
        [Description("Optional plugin name to filter (e.g. 'file', 'git'). Omit for all.")] string? plugin = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Available Capabilities");
        sb.AppendLine();

        var plugins = _kernel.Plugins.AsEnumerable();
        if (!string.IsNullOrEmpty(plugin))
        {
            plugins = plugins.Where(p =>
                p.Name.Equals(plugin, StringComparison.OrdinalIgnoreCase));
        }

        var totalTools = 0;
        foreach (var p in plugins.OrderBy(p => p.Name))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {p.Name}");
            foreach (var fn in p.OrderBy(f => f.Name))
            {
                var desc = fn.Metadata.Description;
                if (desc.Length > 80)
                    desc = string.Concat(desc.AsSpan(0, 77), "...");
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{fn.Name}`: {desc}");
                totalTools++;
            }
            sb.AppendLine();
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"**Total: {totalTools} tools across {_kernel.Plugins.Count} plugins**");
        return sb.ToString();
    }

    [KernelFunction("capability_detail")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Get detailed information about a specific tool including parameters, descriptions, and return type.")]
    public string GetToolDetail(
        [Description("Tool name (e.g. 'read_file', 'git_commit')")] string toolName)
    {
        foreach (var plugin in _kernel.Plugins)
        {
            foreach (var fn in plugin)
            {
                if (fn.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(CultureInfo.InvariantCulture, $"## {fn.Name}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Plugin**: {plugin.Name}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"**Description**: {fn.Metadata.Description}");
                    sb.AppendLine();

                    var parameters = fn.Metadata.Parameters;
                    if (parameters.Count > 0)
                    {
                        sb.AppendLine("### Parameters");
                        sb.AppendLine("| Name | Type | Required | Description |");
                        sb.AppendLine("|------|------|----------|-------------|");
                        foreach (var param in parameters)
                        {
                            var typeName = param.ParameterType?.Name ?? "unknown";
                            var required = param.IsRequired ? "✓" : "✗";
                            var desc = param.Description ?? "-";
                            sb.AppendLine(CultureInfo.InvariantCulture,
                                $"| `{param.Name}` | {typeName} | {required} | {desc} |");
                        }
                    }
                    else
                    {
                        sb.AppendLine("*No parameters*");
                    }

                    var returnType = fn.Metadata.ReturnParameter;
                    if (returnType.ParameterType is not null)
                    {
                        sb.AppendLine();
                        sb.AppendLine(CultureInfo.InvariantCulture,
                            $"**Returns**: {returnType.ParameterType.Name}");
                        if (!string.IsNullOrEmpty(returnType.Description))
                            sb.AppendLine(CultureInfo.InvariantCulture,
                                $"  {returnType.Description}");
                    }

                    return sb.ToString();
                }
            }
        }

        return $"Error: Tool '{toolName}' not found. Use capability_list to see available tools.";
    }

    // ── Usage Analysis ──────────────────────────────────────

    [KernelFunction("capability_usage")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Analyze tool usage patterns in the current session. Shows which tools are used most and which are unused.")]
    public string AnalyzeUsage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Tool Usage Analysis");
        sb.AppendLine();

        var allTools = _kernel.Plugins
            .SelectMany(p => p.Select(f => (Plugin: p.Name, Function: f.Name)))
            .ToList();

        lock (_toolUsage)
        {
            if (_toolUsage.Count == 0)
            {
                sb.AppendLine("No tool usage recorded yet in this session.");
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"**{allTools.Count} tools available** — start using tools to see usage analytics.");
                return sb.ToString();
            }

            // Most used
            sb.AppendLine("### Most Used");
            var sorted = _toolUsage.OrderByDescending(kv => kv.Value).Take(10);
            foreach (var (tool, count) in sorted)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- `{tool}`: {count} call(s)");
            }

            sb.AppendLine();

            // Unused tools
            var usedNames = new HashSet<string>(_toolUsage.Keys, StringComparer.OrdinalIgnoreCase);
            var unused = allTools.Where(t => !usedNames.Contains(t.Function)).ToList();

            sb.AppendLine("### Unused Tools");
            if (unused.Count > 0)
            {
                // Group by plugin
                foreach (var group in unused.GroupBy(t => t.Plugin).OrderBy(g => g.Key))
                {
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"- **{group.Key}**: {string.Join(", ", group.Select(t => $"`{t.Function}`"))}");
                }
            }
            else
            {
                sb.AppendLine("All tools have been used at least once!");
            }

            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"**Summary**: {_toolUsage.Count}/{allTools.Count} tools used, {_toolUsage.Values.Sum()} total calls");
        }

        return sb.ToString();
    }

    // ── Gap Analysis ────────────────────────────────────────

    [KernelFunction("capability_gaps")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Identify missing capability categories compared to common AI agent patterns. Suggests areas for improvement.")]
    public string AnalyzeGaps()
    {
        var availableTools = _kernel.Plugins
            .SelectMany(p => p.Select(f => f.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("## Capability Gap Analysis");
        sb.AppendLine();

        var categories = new Dictionary<string, string[]>
        {
            ["File Operations"] = ["read_file", "write_file", "edit_file", "list_directory"],
            ["Search"] = ["grep", "glob"],
            ["Shell & Build"] = ["run_command", "execute_code"],
            ["Version Control"] = ["git_status", "git_diff", "git_log", "git_commit", "git_push"],
            ["Web & Network"] = ["web_fetch", "web_search"],
            ["Memory & State"] = ["memory_store", "memory_search"],
            ["Browser Automation"] = ["browser_status", "browser_screenshot", "browser_content"],
            ["GitHub Integration"] = ["github_list_issues", "github_list_prs", "github_create_pr"],
            ["Multimodal"] = ["image_analyze", "pdf_analyze", "media_view"],
            ["Orchestration"] = ["spawn_agent", "spawn_team"],
            ["Sessions"] = ["sessions_list", "sessions_spawn"],
            ["Scheduling"] = ["cron_list", "cron_add"],
            ["Channels"] = ["channel_list", "channel_send"],
            ["Gateway"] = ["gateway_status", "gateway_config"],
            ["Introspection"] = ["capability_list", "capability_usage", "think"],
            ["Tasks"] = ["create_task", "list_tasks", "update_task"],
            ["Clipboard"] = ["read_clipboard", "write_clipboard"],
            ["Diagnostics"] = ["get_environment", "get_usage"],
        };

        var covered = 0;
        var total = categories.Count;

        foreach (var (category, tools) in categories.OrderBy(c => c.Key))
        {
            var present = tools.Count(t => availableTools.Contains(t));
            var pct = tools.Length > 0 ? (present * 100) / tools.Length : 0;

            var indicator = pct switch
            {
                100 => "✓",
                > 0 => "◐",
                _ => "✗"
            };

            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- {indicator} **{category}**: {present}/{tools.Length} ({pct}%)");

            if (pct == 100) covered++;
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**Coverage**: {covered}/{total} categories fully covered ({(covered * 100) / total}%)");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**Total tools available**: {availableTools.Count}");

        return sb.ToString();
    }

    // ── Scaffold ────────────────────────────────────────────

    [KernelFunction("capability_scaffold")]
    [ToolSafetyTier(SafetyTier.AutoApprove)]
    [Description("Generate a code template for a new JD.AI tool. Produces a .cs file with KernelFunction stubs.")]
    public static string ScaffoldTool(
        [Description("Tool class name (e.g. 'WeatherTools')")] string className,
        [Description("Plugin namespace suffix (e.g. 'Weather' → JD.AI.Core.Tools)")] string? namespaceSuffix = null,
        [Description("Comma-separated list of tool function names (e.g. 'get_weather,get_forecast')")] string? functions = null)
    {
        var ns = "JD.AI.Core.Tools";
        var fns = functions?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["example_action"];

        var sb = new StringBuilder();
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using Microsoft.SemanticKernel;");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// {className} — auto-generated tool scaffold.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed class {className}");
        sb.AppendLine("{");

        for (var i = 0; i < fns.Length; i++)
        {
            var fnName = fns[i].Trim();
            if (i > 0) sb.AppendLine();

            sb.AppendLine(CultureInfo.InvariantCulture, $"    [KernelFunction(\"{fnName}\")]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    [Description(\"TODO: Describe what {fnName} does.\")]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    public static string {ToPascalCase(fnName)}(");
            sb.AppendLine("        [Description(\"TODO: Describe parameter\")] string input)");
            sb.AppendLine("    {");
            sb.AppendLine("        // TODO: Implement");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        return $\"{{input}} processed by {fnName}\";");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        return $"```csharp\n{sb}\n```\n\nSave to: `src/JD.AI.Core/Tools/{className}.cs`\n" +
               $"Register with: `kernel.Plugins.AddFromType<{className}>(\"{className.Replace("Tools", "").ToLowerInvariant()}\");`";
    }

    private static string ToPascalCase(string snakeCase)
    {
        var parts = snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            string.Concat(char.ToUpperInvariant(p[0]).ToString(), p[1..])));
    }
}
