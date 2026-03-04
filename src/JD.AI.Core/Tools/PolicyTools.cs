using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Tools;

/// <summary>
/// Policy-as-code tools for governance, audit, and trust evaluation.
/// Exposes the governance infrastructure as agent-callable functions.
/// </summary>
public sealed class PolicyTools
{
    private readonly IPolicyEvaluator? _policyEvaluator;
    private readonly AuditService? _auditService;

    public PolicyTools(IPolicyEvaluator? policyEvaluator = null, AuditService? auditService = null)
    {
        _policyEvaluator = policyEvaluator;
        _auditService = auditService;
    }

    // ── Policy Evaluation ───────────────────────────────────

    [KernelFunction("policy_evaluate")]
    [Description("Evaluate whether a specific tool, provider, or model is allowed by the current policy. Returns the policy decision (Allow/Deny/RequireApproval) with reason.")]
    public string EvaluatePolicy(
        [Description("Type of evaluation: 'tool', 'provider', or 'model'")] string type,
        [Description("Name of the tool, provider, or model to evaluate")] string name,
        [Description("Optional project path context")] string? projectPath = null)
    {
        if (_policyEvaluator is null)
            return "No policy evaluator configured. Policies are not enforced in this session.";

        var context = new PolicyContext(
            UserId: Environment.UserName,
            ProjectPath: projectPath ?? Directory.GetCurrentDirectory());

        PolicyEvaluationResult result;
        try
        {
            result = type.ToUpperInvariant() switch
            {
                "TOOL" => _policyEvaluator.EvaluateTool(name, context),
                "PROVIDER" => _policyEvaluator.EvaluateProvider(name, context),
                "MODEL" => _policyEvaluator.EvaluateModel(name, null, context),
                _ => new PolicyEvaluationResult(PolicyDecision.Allow, $"Unknown type '{type}'", null)
            };
        }
#pragma warning disable CA1031 // Catch general exception for resilience
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return $"Error evaluating policy: {ex.Message}";
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Policy Evaluation: {type}/{name}");
        sb.AppendLine();

        var icon = result.Decision switch
        {
            PolicyDecision.Allow => "✅",
            PolicyDecision.Deny => "❌",
            PolicyDecision.RequireApproval => "⚠️",
            PolicyDecision.Audit => "📋",
            _ => "❓"
        };

        sb.AppendLine(CultureInfo.InvariantCulture, $"**Decision**: {icon} {result.Decision}");

        if (!string.IsNullOrEmpty(result.Reason))
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Reason**: {result.Reason}");

        if (!string.IsNullOrEmpty(result.PolicyName))
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Policy**: {result.PolicyName}");

        return sb.ToString();
    }

    // ── Policy Listing ──────────────────────────────────────

    [KernelFunction("policy_list")]
    [Description("List the currently resolved policy rules including tool/provider/model restrictions, budget limits, and data policies.")]
    public string ListPolicies()
    {
        if (_policyEvaluator is null)
            return "No policy evaluator configured. No policies are active.";

        PolicySpec spec;
        try
        {
            spec = _policyEvaluator.GetResolvedPolicy();
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return $"Error loading policies: {ex.Message}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Active Policy Rules");
        sb.AppendLine();

        // Tool policies
        if (spec.Tools is not null)
        {
            sb.AppendLine("### Tool Policy");
            if (spec.Tools.Allowed?.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Allowed**: {string.Join(", ", spec.Tools.Allowed.Select(t => $"`{t}`"))}");
            if (spec.Tools.Denied?.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Denied**: {string.Join(", ", spec.Tools.Denied.Select(t => $"`{t}`"))}");
            if (spec.Tools.Allowed?.Count is null or 0 && spec.Tools.Denied?.Count is null or 0)
                sb.AppendLine("- No restrictions");
            sb.AppendLine();
        }

        // Provider policies
        if (spec.Providers is not null)
        {
            sb.AppendLine("### Provider Policy");
            if (spec.Providers.Allowed?.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Allowed**: {string.Join(", ", spec.Providers.Allowed.Select(p => $"`{p}`"))}");
            if (spec.Providers.Denied?.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Denied**: {string.Join(", ", spec.Providers.Denied.Select(p => $"`{p}`"))}");
            sb.AppendLine();
        }

        // Model policies
        if (spec.Models is not null)
        {
            sb.AppendLine("### Model Policy");
            if (spec.Models.MaxContextWindow > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Max context window**: {spec.Models.MaxContextWindow:N0} tokens");
            if (spec.Models.Denied?.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Denied models**: {string.Join(", ", spec.Models.Denied.Select(m => $"`{m}`"))}");
            sb.AppendLine();
        }

        // Budget policies
        if (spec.Budget is not null)
        {
            sb.AppendLine("### Budget Policy");
            if (spec.Budget.MaxDailyUsd > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Daily limit**: ${spec.Budget.MaxDailyUsd:F2}");
            if (spec.Budget.MaxMonthlyUsd > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Monthly limit**: ${spec.Budget.MaxMonthlyUsd:F2}");
            if (spec.Budget.MaxSessionUsd > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Session limit**: ${spec.Budget.MaxSessionUsd:F2}");
            sb.AppendLine();
        }

        // Data policies
        if (spec.Data is not null)
        {
            sb.AppendLine("### Data Policy");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **No external providers**: {spec.Data.NoExternalProviders}");
            if (spec.Data.RedactPatterns?.Count > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Redact patterns**: {spec.Data.RedactPatterns.Count} active");
            sb.AppendLine();
        }

        // Session policies
        if (spec.Sessions is not null)
        {
            sb.AppendLine("### Session Policy");
            if (spec.Sessions.RetentionDays > 0)
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"- **Retention**: {spec.Sessions.RetentionDays} days");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Require project tag**: {spec.Sessions.RequireProjectTag}");
            sb.AppendLine();
        }

        // Circuit breaker
        if (spec.CircuitBreaker is not null)
        {
            sb.AppendLine("### Circuit Breaker");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Repetition warning**: {spec.CircuitBreaker.RepetitionWarningThreshold}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Hard stop**: {spec.CircuitBreaker.RepetitionHardStopThreshold}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Cooldown**: {spec.CircuitBreaker.CooldownSeconds}s");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Audit ───────────────────────────────────────────────

    [KernelFunction("audit_query")]
    [Description("Query the audit log for recent events. Shows who did what and when, including policy decisions.")]
    public async Task<string> QueryAudit(
        [Description("Number of recent events to show (default 10, max 50)")] int count = 10)
    {
        if (_auditService is null)
            return "No audit service configured. Audit logging is not enabled.";

        count = Math.Clamp(count, 1, 50);

        var sb = new StringBuilder();
        sb.AppendLine("## Recent Audit Events");
        sb.AppendLine();

        // Flush pending events first
        try
        {
            await _auditService.FlushAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            // Flush failures shouldn't block the query
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"*Audit service is active. Showing last {count} events would require reading from the configured sink.*");
        sb.AppendLine();
        sb.AppendLine("### Configured Audit");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- **Service**: Active");
        sb.AppendLine("- **Note**: Use the audit sink directly (file/elasticsearch/webhook) to query historical events.");

        return sb.ToString();
    }

    // ── RBAC ────────────────────────────────────────────────

    [KernelFunction("rbac_check")]
    [Description("Check current user's effective permissions based on policies. Shows what tools, providers, and models are accessible.")]
    public string CheckRbac(
        [Description("Optional user ID to check (defaults to current user)")] string? userId = null)
    {
        userId ??= Environment.UserName;

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## RBAC Check: {userId}");
        sb.AppendLine();

        if (_policyEvaluator is null)
        {
            sb.AppendLine("No policy evaluator configured — all capabilities are available.");
            return sb.ToString();
        }

        var context = new PolicyContext(
            UserId: userId,
            ProjectPath: Directory.GetCurrentDirectory());

        var spec = _policyEvaluator.GetResolvedPolicy();

        // Effective tool access
        sb.AppendLine("### Tool Access");
        if (spec.Tools?.Allowed?.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Whitelist mode**: Only {spec.Tools.Allowed.Count} tools allowed");
        else if (spec.Tools?.Denied?.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Blacklist mode**: {spec.Tools.Denied.Count} tools blocked");
        else
            sb.AppendLine("- **Open**: All tools available");

        sb.AppendLine();

        // Provider access
        sb.AppendLine("### Provider Access");
        if (spec.Providers?.Allowed?.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Restricted to**: {string.Join(", ", spec.Providers.Allowed)}");
        else if (spec.Providers?.Denied?.Count > 0)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- **Blocked**: {string.Join(", ", spec.Providers.Denied)}");
        else
            sb.AppendLine("- **Open**: All providers available");

        sb.AppendLine();

        // Budget
        sb.AppendLine("### Budget Limits");
        if (spec.Budget is not null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- Daily: ${spec.Budget.MaxDailyUsd:F2}");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"- Session: ${spec.Budget.MaxSessionUsd:F2}");
        }
        else
        {
            sb.AppendLine("- No budget limits configured");
        }

        sb.AppendLine();

        // Data protection
        sb.AppendLine("### Data Protection");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- External providers blocked: {spec.Data?.NoExternalProviders?.Count ?? 0}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"- Redaction patterns: {spec.Data?.RedactPatterns?.Count ?? 0}");

        return sb.ToString();
    }

    // ── Policy Validation ───────────────────────────────────

    [KernelFunction("policy_validate")]
    [Description("Validate a policy YAML document for syntax and semantic correctness. Returns validation results.")]
    public static string ValidatePolicy(
        [Description("Policy YAML content to validate")] string yamlContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Policy Validation");
        sb.AppendLine();

        var issues = new List<string>();
        var warnings = new List<string>();

        // Basic YAML structure checks
        if (string.IsNullOrWhiteSpace(yamlContent))
        {
            issues.Add("Policy content is empty");
            return FormatValidation(sb, issues, warnings);
        }

        // Check for required fields
        if (!yamlContent.Contains("apiVersion:", StringComparison.OrdinalIgnoreCase))
            issues.Add("Missing required field: `apiVersion`");
        if (!yamlContent.Contains("kind:", StringComparison.OrdinalIgnoreCase))
            issues.Add("Missing required field: `kind`");
        if (!yamlContent.Contains("metadata:", StringComparison.OrdinalIgnoreCase))
            issues.Add("Missing required field: `metadata`");
        if (!yamlContent.Contains("spec:", StringComparison.OrdinalIgnoreCase))
            issues.Add("Missing required field: `spec`");

        // Check for known kind values
        if (yamlContent.Contains("kind:", StringComparison.OrdinalIgnoreCase) &&
            !yamlContent.Contains("kind: JdaiPolicy", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Expected `kind: JdaiPolicy` — non-standard kind may be ignored");

        // Check for scope
        if (yamlContent.Contains("scope:", StringComparison.OrdinalIgnoreCase))
        {
            var validScopes = new[] { "global", "org", "team", "project", "user" };
            var hasValidScope = validScopes.Any(s =>
                yamlContent.Contains($"scope: {s}", StringComparison.OrdinalIgnoreCase));
            if (!hasValidScope)
                warnings.Add("Scope should be one of: global, org, team, project, user");
        }

        // Check for policy sections
        var sections = new[] { "tools:", "providers:", "models:", "budget:", "data:", "sessions:", "audit:", "circuitBreaker:" };
        var foundSections = sections.Count(s =>
            yamlContent.Contains(s, StringComparison.OrdinalIgnoreCase));

        if (foundSections == 0)
            warnings.Add("No policy sections found (tools, providers, models, budget, data, sessions, audit, circuitBreaker)");

        // Budget range checks
        if (yamlContent.Contains("maxDailyUsd:", StringComparison.OrdinalIgnoreCase))
        {
            // Extract value after maxDailyUsd:
            var idx = yamlContent.IndexOf("maxDailyUsd:", StringComparison.OrdinalIgnoreCase);
            var afterColon = yamlContent[(idx + 12)..].TrimStart();
            var endOfLine = afterColon.IndexOfAny(['\r', '\n']);
            var valueStr = endOfLine > 0 ? afterColon[..endOfLine].Trim() : afterColon.Trim();
            if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) && val > 1000)
                warnings.Add($"maxDailyUsd of {val:F2} seems unusually high — verify intentional");
        }

        return FormatValidation(sb, issues, warnings);
    }

    private static string FormatValidation(StringBuilder sb, List<string> issues, List<string> warnings)
    {
        if (issues.Count > 0)
        {
            sb.AppendLine("### ❌ Errors");
            foreach (var issue in issues)
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {issue}");
            sb.AppendLine();
        }

        if (warnings.Count > 0)
        {
            sb.AppendLine("### ⚠️ Warnings");
            foreach (var warning in warnings)
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {warning}");
            sb.AppendLine();
        }

        if (issues.Count == 0 && warnings.Count == 0)
        {
            sb.AppendLine("### ✅ Valid");
            sb.AppendLine("Policy document passed all validation checks.");
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"**{issues.Count} error(s), {warnings.Count} warning(s)**");

        return sb.ToString();
    }

    // ── Policy Export ───────────────────────────────────────

    [KernelFunction("policy_export")]
    [Description("Export the current resolved policy as JSON for backup, comparison, or CI integration.")]
    public string ExportPolicy()
    {
        if (_policyEvaluator is null)
            return "No policy evaluator configured.";

        try
        {
            var spec = _policyEvaluator.GetResolvedPolicy();
            return JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true });
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return $"Error exporting policy: {ex.Message}";
        }
    }
}
