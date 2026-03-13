using JD.AI.Core.Tools;

namespace JD.AI.Core.Agents;

public enum ToolExecutionGateDecision
{
    Blocked,
    AllowWithoutPrompt,
    RequirePrompt,
}

public readonly record struct ToolExecutionGateResult(
    ToolExecutionGateDecision Decision,
    string? Reason = null);

/// <summary>
/// Shared permission gate for tool execution across structured SK tool calls
/// and text-emitted tool-call fallback execution.
/// </summary>
public static class ToolExecutionPermissionEvaluator
{
    public static ToolExecutionGateResult Evaluate(
        string canonicalToolName,
        PermissionMode permissionMode,
        SafetyTier tier,
        ToolPermissionProfile? profile)
    {
        profile ??= new ToolPermissionProfile();

        if (profile.IsExplicitlyDenied(canonicalToolName))
            return new ToolExecutionGateResult(
                ToolExecutionGateDecision.Blocked,
                "blocked by explicit deny rule");

        if (permissionMode == PermissionMode.Plan && tier != SafetyTier.AutoApprove)
            return new ToolExecutionGateResult(
                ToolExecutionGateDecision.Blocked,
                "plan mode — read-only");

        if (profile.IsExplicitlyAllowed(canonicalToolName))
            return new ToolExecutionGateResult(ToolExecutionGateDecision.AllowWithoutPrompt);

        return new ToolExecutionGateResult(ToolExecutionGateDecision.RequirePrompt);
    }
}
