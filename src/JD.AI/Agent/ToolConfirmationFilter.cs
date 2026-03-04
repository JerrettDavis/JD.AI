using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Rendering;
using JD.AI.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Agent;

/// <summary>
/// SK auto-function-invocation filter that enforces safety tiers,
/// policy-based governance, and renders tool calls to the TUI.
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
                ChatRenderer.RenderWarning($"Policy blocked: {functionName} — {policyResult.Reason}");
                context.Result = new FunctionResult(context.Function, $"Blocked by policy: {policyResult.Reason}");

                await EmitAuditEventAsync(functionName, args, "denied", policyResult).ConfigureAwait(false);
                return;
            }
        }

        // ── Safety tier confirmation ─────────────────────────
        var needsConfirm = !_session.SkipPermissions && !_session.AutoRunEnabled && tier switch
        {
            SafetyTier.AutoApprove => false,
            SafetyTier.ConfirmOnce => !_confirmedOnce.Contains(functionName),
            SafetyTier.AlwaysConfirm => true,
            _ => true,
        };

        if (needsConfirm)
        {
            ChatRenderer.RenderWarning($"Tool: {functionName}({args})");
            if (!ChatRenderer.Confirm("Allow this tool to run?"))
            {
                context.Result = new FunctionResult(context.Function, "User denied tool execution.");
                await EmitAuditEventAsync(functionName, args, "user_denied", policyResult).ConfigureAwait(false);
                return;
            }

            if (tier == SafetyTier.ConfirmOnce)
            {
                _confirmedOnce.Add(functionName);
            }
        }
        else
        {
            ChatRenderer.RenderInfo($"  ▸ {functionName}({args})");
        }

        await next(context).ConfigureAwait(false);

        // Render tool result
        var result = context.Result.GetValue<string>() ?? context.Result.ToString() ?? "";
        ChatRenderer.RenderToolCall(functionName, args, result);

        // ── Audit ────────────────────────────────────────────
        await EmitAuditEventAsync(functionName, args, "ok", policyResult).ConfigureAwait(false);
    }

    private async Task EmitAuditEventAsync(
        string toolName, string args, string status, PolicyEvaluationResult? policyResult)
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
            Detail = $"status={status}; args={args}",
            PolicyResult = policyResult?.Decision,
            Severity = severity,
        }).ConfigureAwait(false);
    }
}
