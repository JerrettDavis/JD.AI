using JD.AI.Tui.Rendering;
using JD.AI.Tui.Tools;
using Microsoft.SemanticKernel;

namespace JD.AI.Tui.Agent;

/// <summary>
/// SK auto-function-invocation filter that enforces safety tiers
/// and renders tool calls to the TUI.
/// </summary>
public sealed class ToolConfirmationFilter : IAutoFunctionInvocationFilter
{
    private readonly AgentSession _session;
    private readonly HashSet<string> _confirmedOnce = new(StringComparer.Ordinal);

    // Safety tier mappings
    private static readonly Dictionary<string, SafetyTier> ToolTiers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = SafetyTier.AutoApprove,
            ["list_directory"] = SafetyTier.AutoApprove,
            ["grep"] = SafetyTier.AutoApprove,
            ["glob"] = SafetyTier.AutoApprove,
            ["git_status"] = SafetyTier.AutoApprove,
            ["git_diff"] = SafetyTier.AutoApprove,
            ["git_log"] = SafetyTier.AutoApprove,
            ["memory_search"] = SafetyTier.AutoApprove,
            ["web_fetch"] = SafetyTier.AutoApprove,

            ["write_file"] = SafetyTier.ConfirmOnce,
            ["edit_file"] = SafetyTier.ConfirmOnce,
            ["git_commit"] = SafetyTier.ConfirmOnce,
            ["memory_store"] = SafetyTier.ConfirmOnce,
            ["memory_forget"] = SafetyTier.ConfirmOnce,

            ["run_command"] = SafetyTier.AlwaysConfirm,
            ["web_search"] = SafetyTier.AlwaysConfirm,
        };

    public ToolConfirmationFilter(AgentSession session)
    {
        _session = session;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var tier = ToolTiers.GetValueOrDefault(functionName, SafetyTier.AlwaysConfirm);

        // Check if we need confirmation
        var needsConfirm = !_session.SkipPermissions && !_session.AutoRunEnabled && tier switch
        {
            SafetyTier.AutoApprove => false,
            SafetyTier.ConfirmOnce => !_confirmedOnce.Contains(functionName),
            SafetyTier.AlwaysConfirm => true,
            _ => true,
        };

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

        if (needsConfirm)
        {
            ChatRenderer.RenderWarning($"Tool: {functionName}({args})");
            if (!ChatRenderer.Confirm("Allow this tool to run?"))
            {
                context.Result = new FunctionResult(context.Function, "User denied tool execution.");
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
    }
}
