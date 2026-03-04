using System.Diagnostics;
using JD.AI.Core.Agents;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Safety;
using JD.AI.Core.Tracing;
using JD.AI.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Agent;

/// <summary>
/// SK auto-function-invocation filter that enforces safety tiers,
/// policy-based governance, and renders tool calls to the TUI via <see cref="IAgentOutput"/>.
/// </summary>
public sealed class ToolConfirmationFilter : IAutoFunctionInvocationFilter
{
    private static readonly ActivitySource ToolActivity = new("JD.AI.Tools");

    private readonly AgentSession _session;
    private readonly IPolicyEvaluator? _policyEvaluator;
    private readonly AuditService? _auditService;
    private readonly CircuitBreaker? _circuitBreaker;
    private readonly HashSet<string> _confirmedOnce = new(StringComparer.Ordinal);

    // Safety tier mappings
    private static readonly Dictionary<string, SafetyTier> ToolTiers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Read-only / no side effects
            ["read_file"] = SafetyTier.AutoApprove,
            ["list_directory"] = SafetyTier.AutoApprove,
            ["grep"] = SafetyTier.AutoApprove,
            ["glob"] = SafetyTier.AutoApprove,
            ["git_status"] = SafetyTier.AutoApprove,
            ["git_diff"] = SafetyTier.AutoApprove,
            ["git_log"] = SafetyTier.AutoApprove,
            ["git_branch"] = SafetyTier.AutoApprove,
            ["memory_search"] = SafetyTier.AutoApprove,
            ["web_fetch"] = SafetyTier.AutoApprove,
            ["ask_questions"] = SafetyTier.AutoApprove,
            ["think"] = SafetyTier.AutoApprove,
            ["get_environment"] = SafetyTier.AutoApprove,
            ["list_tasks"] = SafetyTier.AutoApprove,
            ["export_tasks"] = SafetyTier.AutoApprove,
            ["read_clipboard"] = SafetyTier.AutoApprove,
            ["get_usage"] = SafetyTier.AutoApprove,
            ["create_patch"] = SafetyTier.AutoApprove,
            ["sessions_list"] = SafetyTier.AutoApprove,
            ["sessions_history"] = SafetyTier.AutoApprove,
            ["session_status"] = SafetyTier.AutoApprove,
            ["agents_list"] = SafetyTier.AutoApprove,
            ["channel_list"] = SafetyTier.AutoApprove,
            ["channel_status"] = SafetyTier.AutoApprove,
            ["cron_list"] = SafetyTier.AutoApprove,
            ["cron_history"] = SafetyTier.AutoApprove,
            ["gateway_status"] = SafetyTier.AutoApprove,
            ["gateway_config"] = SafetyTier.AutoApprove,
            ["gateway_channels"] = SafetyTier.AutoApprove,
            ["gateway_agents"] = SafetyTier.AutoApprove,
            ["gateway_sessions"] = SafetyTier.AutoApprove,

            // GitHub tools — read-only ops auto-approve
            ["github_list_issues"] = SafetyTier.AutoApprove,
            ["github_get_issue"] = SafetyTier.AutoApprove,
            ["github_list_prs"] = SafetyTier.AutoApprove,
            ["github_get_pr"] = SafetyTier.AutoApprove,
            ["github_pr_checks"] = SafetyTier.AutoApprove,
            ["github_repo_info"] = SafetyTier.AutoApprove,
            ["github_search_issues"] = SafetyTier.AutoApprove,
            ["github_list_runs"] = SafetyTier.AutoApprove,
            ["github_run_details"] = SafetyTier.AutoApprove,
            ["github_list_releases"] = SafetyTier.AutoApprove,
            ["github_auth_status"] = SafetyTier.AutoApprove,
            ["image_analyze"] = SafetyTier.AutoApprove,
            ["pdf_analyze"] = SafetyTier.AutoApprove,
            ["media_view"] = SafetyTier.AutoApprove,

            // Browser tools — status is read-only
            ["browser_status"] = SafetyTier.AutoApprove,

            // Migration tools — read-only
            ["migration_scan"] = SafetyTier.AutoApprove,
            ["migration_analyze"] = SafetyTier.AutoApprove,
            ["migration_parity"] = SafetyTier.AutoApprove,
            ["migration_export"] = SafetyTier.AutoApprove,
            ["migration_convert"] = SafetyTier.ConfirmOnce,

            // Policy/governance tools — read-only
            ["policy_evaluate"] = SafetyTier.AutoApprove,
            ["policy_list"] = SafetyTier.AutoApprove,
            ["policy_validate"] = SafetyTier.AutoApprove,
            ["policy_export"] = SafetyTier.AutoApprove,
            ["audit_query"] = SafetyTier.AutoApprove,
            ["rbac_check"] = SafetyTier.AutoApprove,

            // Skills parity tools — read-only
            ["skills_parity_matrix"] = SafetyTier.AutoApprove,
            ["skills_pack_overview"] = SafetyTier.AutoApprove,
            ["skills_gap_analysis"] = SafetyTier.AutoApprove,
            ["skills_detail"] = SafetyTier.AutoApprove,
            ["skills_parity_export"] = SafetyTier.AutoApprove,

            // Parity documentation tools — read-only
            ["parity_compatibility_matrix"] = SafetyTier.AutoApprove,
            ["parity_migration_guide"] = SafetyTier.AutoApprove,
            ["parity_governance_runbook"] = SafetyTier.AutoApprove,
            ["parity_threat_model"] = SafetyTier.AutoApprove,
            ["parity_export"] = SafetyTier.AutoApprove,

            // Benchmark tools — read-only introspection
            ["benchmark_scorecard"] = SafetyTier.AutoApprove,
            ["benchmark_export"] = SafetyTier.AutoApprove,
            ["benchmark_regression"] = SafetyTier.AutoApprove,
            ["benchmark_run"] = SafetyTier.ConfirmOnce, // invokes tools

            // Write ops — confirm once per session
            ["write_file"] = SafetyTier.ConfirmOnce,
            ["edit_file"] = SafetyTier.ConfirmOnce,
            ["git_commit"] = SafetyTier.ConfirmOnce,
            ["git_push"] = SafetyTier.ConfirmOnce,
            ["git_pull"] = SafetyTier.ConfirmOnce,
            ["git_checkout"] = SafetyTier.ConfirmOnce,
            ["git_stash"] = SafetyTier.ConfirmOnce,
            ["memory_store"] = SafetyTier.ConfirmOnce,
            ["memory_forget"] = SafetyTier.ConfirmOnce,
            ["create_task"] = SafetyTier.ConfirmOnce,
            ["update_task"] = SafetyTier.ConfirmOnce,
            ["complete_task"] = SafetyTier.ConfirmOnce,
            ["write_clipboard"] = SafetyTier.ConfirmOnce,
            ["spawn_agent"] = SafetyTier.ConfirmOnce,
            ["spawn_team"] = SafetyTier.ConfirmOnce,
            ["sessions_spawn"] = SafetyTier.ConfirmOnce,
            ["sessions_send"] = SafetyTier.ConfirmOnce,
            ["cron_add"] = SafetyTier.ConfirmOnce,
            ["cron_update"] = SafetyTier.ConfirmOnce,
            ["cron_remove"] = SafetyTier.ConfirmOnce,
            ["cron_run"] = SafetyTier.ConfirmOnce,
            ["channel_send"] = SafetyTier.ConfirmOnce,
            ["channel_connect"] = SafetyTier.ConfirmOnce,
            ["channel_disconnect"] = SafetyTier.ConfirmOnce,
            ["apply_patch"] = SafetyTier.ConfirmOnce,
            ["batch_edit_files"] = SafetyTier.ConfirmOnce,
            ["reset_usage"] = SafetyTier.ConfirmOnce,

            // GitHub tools — write ops confirm once
            ["github_create_issue"] = SafetyTier.ConfirmOnce,
            ["github_close_issue"] = SafetyTier.ConfirmOnce,
            ["github_create_pr"] = SafetyTier.ConfirmOnce,
            ["github_merge_pr"] = SafetyTier.ConfirmOnce,
            ["github_pr_review"] = SafetyTier.ConfirmOnce,

            // Browser tools — write/navigate ops confirm once
            ["browser_open"] = SafetyTier.ConfirmOnce,
            ["browser_screenshot"] = SafetyTier.ConfirmOnce,
            ["browser_pdf"] = SafetyTier.ConfirmOnce,
            ["browser_content"] = SafetyTier.ConfirmOnce,
            ["browser_console"] = SafetyTier.ConfirmOnce,

            // Capability introspection — read-only
            ["capability_list"] = SafetyTier.AutoApprove,
            ["capability_detail"] = SafetyTier.AutoApprove,
            ["capability_usage"] = SafetyTier.AutoApprove,
            ["capability_gaps"] = SafetyTier.AutoApprove,
            ["capability_scaffold"] = SafetyTier.AutoApprove,

            // Dangerous — always confirm
            ["run_command"] = SafetyTier.AlwaysConfirm,
            ["web_search"] = SafetyTier.AlwaysConfirm,
            ["execute_code"] = SafetyTier.AlwaysConfirm,
        };

    public ToolConfirmationFilter(
        AgentSession session,
        IPolicyEvaluator? policyEvaluator = null,
        AuditService? auditService = null,
        CircuitBreaker? circuitBreaker = null)
    {
        _session = session;
        _policyEvaluator = policyEvaluator;
        _auditService = auditService;
        _circuitBreaker = circuitBreaker;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var tier = ToolTiers.GetValueOrDefault(functionName, SafetyTier.AlwaysConfirm);
        var output = AgentOutput.Current;

        // Check if we need confirmation based on permission mode
        bool blocked = false;
        var needsConfirm = false;

        switch (_session.PermissionMode)
        {
            case PermissionMode.Plan:
                // Read-only: block anything above AutoApprove
                if (tier != SafetyTier.AutoApprove)
                {
                    blocked = true;
                }
                break;
            case PermissionMode.AcceptEdits:
                // Auto-approve file writes (ConfirmOnce), still confirm shell (AlwaysConfirm)
                needsConfirm = tier == SafetyTier.AlwaysConfirm;
                break;
            case PermissionMode.BypassAll:
                // Skip everything
                break;
            default: // Normal
                needsConfirm = !_session.SkipPermissions && !_session.AutoRunEnabled && tier switch
                {
                    SafetyTier.AutoApprove => false,
                    SafetyTier.ConfirmOnce => !_confirmedOnce.Contains(functionName),
                    SafetyTier.AlwaysConfirm => true,
                    _ => true,
                };
                break;
        }

        if (blocked)
        {
            output.RenderWarning($"  ✗ {functionName} blocked (plan mode — read-only)");
            context.Result = new FunctionResult(context.Function, "Tool blocked: plan mode restricts to read-only operations.");
            return;
        }

        // Build argument summary for display
        var args = string.Join(", ", (context.Arguments ?? [])
            .Select(kv =>
            {
                var val = kv.Value?.ToString() ?? "null";
                if (val.Length > 80)
                {
                    val = string.Concat(val.AsSpan(0, 77), "...");
                }
                return $"{kv.Key}={val}";
            }));

        // ── Policy evaluation ────────────────────────────────
        PolicyEvaluationResult? policyResult = null;
        if (_policyEvaluator is not null)
        {
            policyResult = _policyEvaluator.EvaluateTool(functionName, new PolicyContext(
                ProjectPath: _session.SessionInfo?.ProjectPath));

            if (policyResult.Decision == PolicyDecision.Deny)
            {
                output.RenderWarning($"Policy blocked: {functionName} — {policyResult.Reason}");
                context.Result = new FunctionResult(context.Function, $"Blocked by policy: {policyResult.Reason}");

                await EmitAuditEventAsync(functionName, context.Arguments, "denied", policyResult).ConfigureAwait(false);
                return;
            }
        }

        // ── Circuit breaker / loop detection ────────────────
        if (_circuitBreaker is not null)
        {
            var argsHash = args.GetHashCode(StringComparison.Ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var cbResult = _circuitBreaker.Evaluate(functionName, argsHash, agentId: _session.SessionInfo?.Id);

            if (cbResult.Action == CircuitAction.Block)
            {
                output.RenderWarning($"  ⚡ Circuit breaker: {cbResult.Message}");
                output.RenderInfo("  💡 Hint: Try a different approach or use /circuit-reset to manually reset.");
                context.Result = new FunctionResult(context.Function,
                    $"Blocked by circuit breaker: {cbResult.Message}");

                Telemetry.Meters.CircuitBreakerTrips.Add(1,
                    new KeyValuePair<string, object?>("jdai.tool.name", functionName));

                await EmitAuditEventAsync(functionName, context.Arguments, "circuit_breaker_block", policyResult).ConfigureAwait(false);
                return;
            }

            if (cbResult.Action == CircuitAction.Warn)
            {
                output.RenderWarning($"  ⚠ Loop warning: {cbResult.Message}");

                Telemetry.Meters.LoopDetections.Add(1,
                    new KeyValuePair<string, object?>("jdai.tool.name", functionName),
                    new KeyValuePair<string, object?>("jdai.safety.decision", "warning"));
            }
        }

        // ── Safety tier confirmation (already computed above via PermissionMode) ──

        if (needsConfirm)
        {
            if (!output.ConfirmToolCall(functionName, args))
            {
                context.Result = new FunctionResult(context.Function, "User denied tool execution.");
                await EmitAuditEventAsync(functionName, context.Arguments, "user_denied", policyResult).ConfigureAwait(false);
                return;
            }

            if (tier == SafetyTier.ConfirmOnce)
            {
                _confirmedOnce.Add(functionName);
            }
        }
        else
        {
            output.RenderInfo($"  ▸ {functionName}({args})");
        }

        // ── Tool execution with OTel + timeline tracing ─────────────────
        using var activity = ToolActivity.StartActivity("jdai.tool.invoke");
        activity?.SetTag("jdai.tool.name", functionName);
        activity?.SetTag("jdai.tool.safety_tier", tier.ToString());
        activity?.SetTag("jdai.tool.permission_mode", _session.PermissionMode.ToString());
        if (_circuitBreaker is not null)
        {
            activity?.SetTag("jdai.safety.circuit_state", _circuitBreaker.State.ToString());
        }

        var timeline = TraceContext.CurrentContext.Timeline;
        var timelineEntry = timeline.BeginOperation(
            $"tool.{functionName}",
            attributes: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["safety_tier"] = tier.ToString(),
            });

        var sw = Stopwatch.StartNew();
        await next(context).ConfigureAwait(false);
        sw.Stop();

        timelineEntry.Complete();
        DebugLogger.Log(DebugCategory.Tools, "{0}: args={1}, duration={2}ms",
            functionName, args, sw.ElapsedMilliseconds);

        activity?.SetTag("jdai.tool.duration_ms", sw.ElapsedMilliseconds);
        activity?.SetStatus(ActivityStatusCode.Ok);

        // Record metric
        JD.AI.Telemetry.Meters.ToolCalls.Add(1,
            new KeyValuePair<string, object?>("jdai.tool.name", functionName));

        // Render tool result
        var result = context.Result.GetValue<string>() ?? context.Result.ToString() ?? "";
        output.RenderToolCall(functionName, args, result);

        // ── Audit ────────────────────────────────────────────
        await EmitAuditEventAsync(functionName, context.Arguments, "ok", policyResult).ConfigureAwait(false);
    }

    // Argument keys whose values should not be logged in audit events
    private static readonly HashSet<string> RedactedArgKeys =
        new(StringComparer.OrdinalIgnoreCase) { "content", "code", "input", "body", "password", "secret", "token" };

    private async Task EmitAuditEventAsync(
        string toolName, KernelArguments? arguments, string status, PolicyEvaluationResult? policyResult)
    {
        if (_auditService is null) return;

        var severity = status switch
        {
            "denied" => AuditSeverity.Warning,
            "user_denied" => AuditSeverity.Info,
            _ => AuditSeverity.Debug,
        };

        await _auditService.EmitAsync(new AuditEvent
        {
            Action = "tool.invoke",
            Resource = toolName,
            SessionId = _session.SessionInfo?.Id,
            TraceId = Activity.Current?.TraceId.ToString(),
            Detail = $"status={status}; args={BuildRedactedArgs(arguments)}",
            PolicyResult = policyResult?.Decision,
            Severity = severity,
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a redacted argument string from structured KernelArguments.
    /// Redacts at the key/value level to avoid delimiter-based parsing issues.
    /// </summary>
    internal static string BuildRedactedArgs(KernelArguments? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return "";

        return string.Join(", ", arguments.Select(kv =>
        {
            if (RedactedArgKeys.Contains(kv.Key))
                return $"{kv.Key}=[REDACTED]";

            var val = kv.Value?.ToString() ?? "null";
            if (val.Length > 80)
                val = string.Concat(val.AsSpan(0, 77), "...");

            return $"{kv.Key}={val}";
        }));
    }
}
