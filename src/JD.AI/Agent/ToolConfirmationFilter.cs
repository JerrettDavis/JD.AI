using System.Diagnostics;
using JD.AI.Core.Agents;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Safety;
using JD.AI.Core.Tools;
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

    // Safety tier mappings — built from [ToolSafetyTier] attributes via assembly scanning
    internal static readonly IReadOnlyDictionary<string, SafetyTier> ToolTierMap =
        ToolAssemblyScanner.BuildSafetyTierMap(typeof(FileTools).Assembly, typeof(SubagentTools).Assembly);

    internal static string ResolvePolicyToolName(string functionName) =>
        OpenClawToolAliasResolver.Resolve(functionName);

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
        var canonicalToolName = ResolvePolicyToolName(functionName);
        var tier = ToolTierMap.GetValueOrDefault(canonicalToolName, SafetyTier.AlwaysConfirm);
        var output = AgentOutput.Current;
        var gate = ToolExecutionPermissionEvaluator.Evaluate(
            canonicalToolName,
            _session.PermissionMode,
            tier,
            _session.ToolPermissionProfile);

        // ── Workflow enforcement ────────────────────────────
        // If a workflow is active, tool calls are coordinated — skip the prompt.
        // If AutoApprove (read-only), let it through freely.
        // If the user already declined this turn, don't nag again.
        // Otherwise, prompt the user to start a workflow.
        if (gate.Decision == ToolExecutionGateDecision.RequirePrompt &&
            _session.ActiveWorkflowName is null &&
            !_session.WorkflowDeclinedThisTurn &&
            tier != SafetyTier.AutoApprove &&
            _session.PermissionMode != PermissionMode.BypassAll)
        {
            if (output.ConfirmWorkflowPrompt(functionName))
            {
                // User wants a workflow — start capturing tool calls
                _session.ActiveWorkflowName = "recording";
                _session.CapturedWorkflowSteps.Clear();
                output.RenderInfo("📋 Recording workflow — tool calls will be captured.");
            }
            else
            {
                // User declined — remember for this turn
                _session.WorkflowDeclinedThisTurn = true;
            }
        }

        // ── Workflow capture ────────────────────────────
        // If a workflow is being recorded, capture this tool call.
        if (_session.ActiveWorkflowName is not null)
        {
            var captureArgs = string.Join(", ", (context.Arguments ?? [])
                .Select(kv => $"{kv.Key}={kv.Value?.ToString() ?? "null"}"));
            _session.CapturedWorkflowSteps.Add((canonicalToolName, captureArgs));
        }

        // Check if we need confirmation based on permission mode
        bool blocked = false;
        var needsConfirm = false;
        var blockReason = string.Empty;

        switch (gate.Decision)
        {
            case ToolExecutionGateDecision.Blocked:
                blocked = true;
                blockReason = gate.Reason ?? "blocked by permission policy";
                break;
            case ToolExecutionGateDecision.AllowWithoutPrompt:
                needsConfirm = false;
                break;
            case ToolExecutionGateDecision.RequirePrompt:
                needsConfirm = true;
                break;
        }

        if (blocked)
        {
            output.RenderWarning($"  ✗ {functionName} blocked ({blockReason})");
            var blockedResult = $"Tool blocked: {blockReason}.";
            context.Result = new FunctionResult(context.Function, blockedResult);
            _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(context.Arguments), blockedResult, "denied", 0);
            return;
        }

        // Build argument summary for display
        var args = string.Join(", ", (context.Arguments ?? [])
            .Select(kv =>
            {
                var val = kv.Value?.ToString() ?? "null";
                if (val.Length > 200)
                {
                    val = string.Concat(val.AsSpan(0, 197), "...");
                }
                return $"{kv.Key}={val}";
            }));

        // Enhanced display for file operations — show path + content size
        var displayArgs = args;
        if (canonicalToolName.Contains("write_file", StringComparison.Ordinal) ||
            canonicalToolName.Contains("edit_file", StringComparison.Ordinal))
        {
            var path = context.Arguments?["path"]?.ToString() ?? context.Arguments?["filePath"]?.ToString();
            var content = context.Arguments?["content"]?.ToString();
            if (path is not null)
            {
                var sizeInfo = content is not null ? $" [{content.Length} chars]" : "";
                var preview = BuildContentPreview(content);
                displayArgs = preview is null
                    ? $"path={path}{sizeInfo}"
                    : $"path={path}{sizeInfo}, preview=\"{preview}\"";
            }
        }

        // ── Policy evaluation ────────────────────────────────
        PolicyEvaluationResult? policyResult = null;
        if (_policyEvaluator is not null)
        {
            policyResult = _policyEvaluator.EvaluateTool(canonicalToolName, new PolicyContext(
                ProjectPath: _session.SessionInfo?.ProjectPath));

            if (policyResult.Decision == PolicyDecision.Deny)
            {
                output.RenderWarning($"Policy blocked: {functionName} — {policyResult.Reason}");
                var deniedResult = $"Blocked by policy: {policyResult.Reason}";
                context.Result = new FunctionResult(context.Function, deniedResult);
                _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(context.Arguments), deniedResult, "denied", 0);

                await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "denied", policyResult)
                    .ConfigureAwait(false);
                return;
            }
        }

        // ── Circuit breaker / loop detection ────────────────
        if (_circuitBreaker is not null)
        {
            var argsHash = args.GetHashCode(StringComparison.Ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var cbResult = _circuitBreaker.Evaluate(canonicalToolName, argsHash, agentId: _session.SessionInfo?.Id);

            if (cbResult.Action == CircuitAction.Block)
            {
                output.RenderWarning($"  ⚡ Circuit breaker: {cbResult.Message}");
                output.RenderInfo("  💡 Hint: Try a different approach or use /circuit-reset to manually reset.");
                var blockedResult = $"Blocked by circuit breaker: {cbResult.Message}";
                context.Result = new FunctionResult(context.Function, blockedResult);
                _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(context.Arguments), blockedResult, "denied", 0);

                Telemetry.Meters.CircuitBreakerTrips.Add(1,
                    new KeyValuePair<string, object?>("jdai.tool.name", functionName),
                    new KeyValuePair<string, object?>("jdai.tool.canonical_name", canonicalToolName));

                await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "circuit_breaker_block", policyResult)
                    .ConfigureAwait(false);
                return;
            }

            if (cbResult.Action == CircuitAction.Warn)
            {
                output.RenderWarning($"  ⚠ Loop warning: {cbResult.Message}");

                Telemetry.Meters.LoopDetections.Add(1,
                    new KeyValuePair<string, object?>("jdai.tool.name", functionName),
                    new KeyValuePair<string, object?>("jdai.tool.canonical_name", canonicalToolName),
                    new KeyValuePair<string, object?>("jdai.safety.decision", "warning"));
            }
        }

        // ── Safety tier confirmation (already computed above via PermissionMode) ──

        if (needsConfirm)
        {
            if (!output.ConfirmToolCall(functionName, displayArgs))
            {
                const string deniedResult = "User denied tool execution.";
                context.Result = new FunctionResult(context.Function, deniedResult);
                _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(context.Arguments), deniedResult, "denied", 0);
                await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "user_denied", policyResult)
                    .ConfigureAwait(false);
                return;
            }

            if (tier == SafetyTier.ConfirmOnce)
            {
                _confirmedOnce.Add(canonicalToolName);
            }
        }
        else
        {
            output.RenderInfo($"  ▸ {functionName}({displayArgs})");
        }

        // ── Tool execution with OTel + timeline tracing ─────────────────
        using var activity = ToolActivity.StartActivity("jdai.tool.invoke");
        activity?.SetTag("jdai.tool.name", functionName);
        activity?.SetTag("jdai.tool.canonical_name", canonicalToolName);
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
            new KeyValuePair<string, object?>("jdai.tool.name", functionName),
            new KeyValuePair<string, object?>("jdai.tool.canonical_name", canonicalToolName));

        // Render tool result
        var result = context.Result.GetValue<string>() ?? context.Result.ToString() ?? "";
        output.RenderToolCall(functionName, displayArgs, result);
        _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(context.Arguments), result, "ok", sw.ElapsedMilliseconds);

        // ── Audit ────────────────────────────────────────────
        await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "ok", policyResult)
            .ConfigureAwait(false);
    }

    // Argument keys whose values should not be logged in audit events
    private static readonly HashSet<string> RedactedArgKeys =
        new(StringComparer.OrdinalIgnoreCase) { "content", "code", "input", "body", "password", "secret", "token" };

    private async Task EmitAuditEventAsync(
        string toolName,
        string canonicalToolName,
        KernelArguments? arguments,
        string status,
        PolicyEvaluationResult? policyResult)
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
            Resource = canonicalToolName,
            SessionId = _session.SessionInfo?.Id,
            TraceId = Activity.Current?.TraceId.ToString(),
            Detail = $"status={status}; alias={toolName}; canonical={canonicalToolName}; args={BuildRedactedArgs(arguments)}",
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

    internal static string? BuildContentPreview(string? content, int maxChars = 120)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var singleLine = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(singleLine))
            return null;

        return singleLine.Length <= maxChars
            ? singleLine
            : string.Concat(singleLine.AsSpan(0, maxChars - 3), "...");
    }
}
