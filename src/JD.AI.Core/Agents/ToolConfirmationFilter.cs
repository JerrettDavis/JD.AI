using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JD.AI.Core.Agents;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using JD.AI.Core.Infrastructure;
using JD.AI.Core.Safety;
using JD.AI.Core.Tools;
using JD.AI.Core.Tracing;
using Microsoft.SemanticKernel;

namespace JD.AI.Core.Agents;

/// <summary>
/// SK auto-function-invocation filter that enforces safety tiers,
/// policy-based governance, and renders tool calls to the TUI via <see cref="IAgentOutput"/>.
/// </summary>
public sealed class ToolConfirmationFilter : IAutoFunctionInvocationFilter
{
    private readonly AgentSession _session;
    private readonly IPolicyEvaluator? _policyEvaluator;
    private readonly AuditService? _auditService;
    private readonly CircuitBreaker? _circuitBreaker;
    private readonly IAgentOutput? _output;

    // Safety tier mappings — built from [ToolSafetyTier] attributes via assembly scanning
    internal static readonly IReadOnlyDictionary<string, SafetyTier> ToolTierMap =
        ToolAssemblyScanner.BuildSafetyTierMap(typeof(ToolAssemblyScanner).Assembly);

    internal static string ResolvePolicyToolName(string functionName) =>
        OpenClawToolAliasResolver.Resolve(functionName);

    /// <summary>
    /// The active output renderer for the current turn.
    /// </summary>
    public IAgentOutput? Output { get; set; }

    public ToolConfirmationFilter(
        AgentSession session,
        IPolicyEvaluator? policyEvaluator = null,
        AuditService? auditService = null,
        CircuitBreaker? circuitBreaker = null,
        IAgentOutput? output = null)
    {
        _session = session;
        _policyEvaluator = policyEvaluator;
        _auditService = auditService;
        _circuitBreaker = circuitBreaker;
        _output = output;
        Output = output;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var canonicalToolName = ResolvePolicyToolName(functionName);
        var tier = ToolTierMap.GetValueOrDefault(canonicalToolName, SafetyTier.AlwaysConfirm);
        var output = Output ?? _output ?? AgentOutput.Current;
        var effectivePermissionMode = (_session.SkipPermissions || _session.AutoRunEnabled)
            ? PermissionMode.BypassAll
            : _session.PermissionMode;
        var gate = ToolExecutionPermissionEvaluator.Evaluate(
            canonicalToolName,
            effectivePermissionMode,
            tier,
            _session.ToolPermissionProfile);

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

        if (needsConfirm &&
            tier == SafetyTier.ConfirmOnce &&
            _session.ConfirmedOnceTools.Contains(canonicalToolName))
        {
            needsConfirm = false;
        }

        // ── Workflow enforcement ────────────────────────────
        if (needsConfirm &&
            _session.ActiveWorkflowName is null &&
            !_session.WorkflowDeclinedThisTurn &&
            tier != SafetyTier.AutoApprove &&
            effectivePermissionMode != PermissionMode.BypassAll)
        {
            if (output.ConfirmWorkflowPrompt(functionName))
            {
                _session.ActiveWorkflowName = "recording";
                _session.CapturedWorkflowSteps.Clear();
                output.RenderInfo("📋 Recording workflow — tool calls will be captured.");
            }
            else
            {
                _session.WorkflowDeclinedThisTurn = true;
            }
        }

        var gateDecision = blocked
            ? ToolExecutionGateDecision.Blocked
            : needsConfirm
                ? ToolExecutionGateDecision.RequirePrompt
                : ToolExecutionGateDecision.AllowWithoutPrompt;
        ToolExecutionPermissionEvaluator.PublishAuditDecision(
            canonicalToolName,
            gateDecision,
            _session.EventBus,
            _session.SessionInfo?.Id);

        if (blocked)
        {
            output.RenderWarning($"  ✗ {functionName} blocked ({blockReason})");
            var blockedResult = $"Tool blocked: {blockReason}.";
            context.Result = new FunctionResult(context.Function, blockedResult);
            _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(canonicalToolName, context.Arguments), blockedResult, "denied", 0);
            return;
        }

        var displayArgs = BuildDisplayArgs(canonicalToolName, context.Arguments);
        var persistedArgs = BuildPersistedArgs(canonicalToolName, context.Arguments);
        var argsFingerprint = BuildArgsFingerprint(canonicalToolName, context.Arguments);

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
                _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(canonicalToolName, context.Arguments), deniedResult, "denied", 0);

                await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "denied", policyResult)
                    .ConfigureAwait(false);
                return;
            }
        }

        if (_circuitBreaker is not null)
        {
            var cbResult = _circuitBreaker.Evaluate(canonicalToolName, argsFingerprint, agentId: _session.SessionInfo?.Id);

            if (cbResult.Action == CircuitAction.Block)
            {
                output.RenderWarning($"  ⚡ Circuit breaker: {cbResult.Message}");
                var blockedResult = $"Blocked by circuit breaker: {cbResult.Message}";
                context.Result = new FunctionResult(context.Function, blockedResult);
                _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(canonicalToolName, context.Arguments), blockedResult, "denied", 0);

                AgentInstrumentation.CircuitBreakerTrips.Add(1,
                    new KeyValuePair<string, object?>("jdai.tool.name", functionName),
                    new KeyValuePair<string, object?>("jdai.tool.canonical_name", canonicalToolName));

                await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "circuit_breaker_block", policyResult)
                    .ConfigureAwait(false);
                return;
            }

            if (cbResult.Action == CircuitAction.Warn)
            {
                output.RenderWarning($"  ⚠ Loop warning: {cbResult.Message}");
                AgentInstrumentation.LoopDetections.Add(1,
                    new KeyValuePair<string, object?>("jdai.tool.name", functionName),
                    new KeyValuePair<string, object?>("jdai.tool.canonical_name", canonicalToolName),
                    new KeyValuePair<string, object?>("jdai.safety.decision", "warning"));
            }
        }

        if (needsConfirm)
        {
            if (!output.ConfirmToolCall(functionName, displayArgs))
            {
                const string deniedResult = "User denied tool execution.";
                context.Result = new FunctionResult(context.Function, deniedResult);
                _session.RecordToolCall(canonicalToolName, BuildRedactedArgs(canonicalToolName, context.Arguments), deniedResult, "denied", 0);
                await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "user_denied", policyResult)
                    .ConfigureAwait(false);
                return;
            }

            if (tier == SafetyTier.ConfirmOnce)
            {
                _session.ConfirmedOnceTools.Add(canonicalToolName);
            }
        }
        else
        {
            output.RenderInfo($"  ▸ {functionName}({displayArgs})");
        }

        var sw = Stopwatch.StartNew();
        await next(context).ConfigureAwait(false);
        sw.Stop();

        if (string.Equals(_session.ActiveWorkflowName, "recording", StringComparison.Ordinal))
        {
            _session.CapturedWorkflowSteps.Add((canonicalToolName, persistedArgs));
        }

        // Record metrics
        AgentInstrumentation.ToolCalls.Add(1,
            new KeyValuePair<string, object?>("jdai.tool.name", functionName),
            new KeyValuePair<string, object?>("jdai.tool.canonical_name", canonicalToolName));

        var redactedArgs = BuildRedactedArgs(canonicalToolName, context.Arguments);
        _session.TryRegisterToolCallForCurrentTurn(canonicalToolName, argsFingerprint);
        var result = context.Result.GetValue<string>() ?? context.Result.ToString() ?? "";
        output.RenderToolCall(functionName, displayArgs, result);
        _session.RecordToolCall(canonicalToolName, redactedArgs, result, "ok", sw.ElapsedMilliseconds);

        await EmitAuditEventAsync(functionName, canonicalToolName, context.Arguments, "ok", policyResult)
            .ConfigureAwait(false);
    }

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
            Detail = $"status={status}; alias={toolName}; canonical={canonicalToolName}; args={BuildRedactedArgs(canonicalToolName, arguments)}",
            PolicyResult = policyResult?.Decision,
            Severity = severity,
        }).ConfigureAwait(false);
    }

    internal static string BuildRedactedArgs(KernelArguments? arguments)
        => BuildRedactedArgs(null, arguments);

    internal static string BuildRedactedArgs(string? canonicalToolName, KernelArguments? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return "";

        if (string.Equals(canonicalToolName, "edit_file", StringComparison.OrdinalIgnoreCase))
        {
            var path = arguments["path"]?.ToString() ?? "null";
            return $"path={path}, oldStr=[REDACTED], newStr=[REDACTED]";
        }

        if (string.Equals(canonicalToolName, "batch_edit_files", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonicalToolName, "apply_patch", StringComparison.OrdinalIgnoreCase))
        {
            return "editsJson=[REDACTED]";
        }

        if (string.Equals(canonicalToolName, "run_command", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonicalToolName, "bash", StringComparison.OrdinalIgnoreCase))
        {
            return "command=[REDACTED]";
        }

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

    internal static string BuildDisplayArgs(string canonicalToolName, KernelArguments? arguments)
    {
        var args = string.Join(", ", (arguments ?? [])
            .Select(kv =>
            {
                var val = kv.Value?.ToString() ?? "null";
                if (val.Length > 200)
                {
                    val = string.Concat(val.AsSpan(0, 197), "...");
                }

                return $"{kv.Key}={val}";
            }));

        if (canonicalToolName.Contains("write_file", StringComparison.Ordinal) ||
            canonicalToolName.Contains("edit_file", StringComparison.Ordinal))
        {
            var path = arguments?["path"]?.ToString() ?? arguments?["filePath"]?.ToString();
            var content = arguments?["content"]?.ToString();
            if (path is not null)
            {
                var sizeInfo = content is not null ? $" [{content.Length} chars]" : "";
                return $"path={path}{sizeInfo}";
            }
        }

        if (string.Equals(canonicalToolName, "batch_edit_files", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonicalToolName, "apply_patch", StringComparison.OrdinalIgnoreCase))
        {
            return "editsJson=[REDACTED]";
        }

        return args;
    }

    internal static string BuildPersistedArgs(string canonicalToolName, KernelArguments? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return "";

        if (string.Equals(canonicalToolName, "run_command", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonicalToolName, "bash", StringComparison.OrdinalIgnoreCase))
        {
            return "command=[REDACTED]";
        }

        if (canonicalToolName.Contains("write_file", StringComparison.Ordinal) ||
            canonicalToolName.Contains("edit_file", StringComparison.Ordinal) ||
            string.Equals(canonicalToolName, "batch_edit_files", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonicalToolName, "apply_patch", StringComparison.OrdinalIgnoreCase))
        {
            return BuildDisplayArgs(canonicalToolName, arguments);
        }

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

    internal static string BuildArgsFingerprint(string canonicalToolName, KernelArguments? arguments)
    {
        var normalized = string.Join("|", (arguments ?? [])
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value?.ToString() ?? "null"}"));
        var payload = $"{canonicalToolName.ToLowerInvariant()}|{normalized}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture);
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
