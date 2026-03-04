using System.Diagnostics;
using JD.AI.Core.Agents;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Agent;

/// <summary>
/// SK auto-function-invocation filter that enforces safety tiers,
/// policy-based governance, and renders tool calls to the TUI via <see cref="IAgentOutput"/>.
/// </summary>
public sealed class ToolConfirmationFilter : IAutoFunctionInvocationFilter
{
    private readonly AgentSession _session;
    private readonly IPolicyEvaluator? _policyEvaluator;
    private readonly AuditService? _auditService;
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
            ["apply_patch"] = SafetyTier.ConfirmOnce,
            ["batch_edit_files"] = SafetyTier.ConfirmOnce,
            ["reset_usage"] = SafetyTier.ConfirmOnce,

            // Dangerous — always confirm
            ["run_command"] = SafetyTier.AlwaysConfirm,
            ["web_search"] = SafetyTier.AlwaysConfirm,
            ["execute_code"] = SafetyTier.AlwaysConfirm,
        };

    public ToolConfirmationFilter(
        AgentSession session,
        IPolicyEvaluator? policyEvaluator = null,
        AuditService? auditService = null)
    {
        _session = session;
        _policyEvaluator = policyEvaluator;
        _auditService = auditService;
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

        await next(context).ConfigureAwait(false);

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
