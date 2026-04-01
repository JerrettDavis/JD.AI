using JD.AI.Core.Events;
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

        // AutoApprove-tier tools bypass prompts regardless of permission mode.
        // The Plan-mode block below only applies to AlwaysConfirm/ConfirmOnce tools.
        if (tier == SafetyTier.AutoApprove)
            return new ToolExecutionGateResult(ToolExecutionGateDecision.AllowWithoutPrompt);

        if (permissionMode == PermissionMode.Plan)
            return new ToolExecutionGateResult(
                ToolExecutionGateDecision.Blocked,
                "plan mode — read-only");

        if (profile.IsExplicitlyAllowed(canonicalToolName))
            return new ToolExecutionGateResult(ToolExecutionGateDecision.AllowWithoutPrompt);

        return new ToolExecutionGateResult(ToolExecutionGateDecision.RequirePrompt);
    }

    /// <summary>
    /// Overload that emits an audit event via <paramref name="eventBus"/> for every
    /// tool execution decision. The event is published fire-and-forget so it does not
    /// block the caller.
    /// </summary>
    public static ToolExecutionGateResult Evaluate(
        string canonicalToolName,
        PermissionMode permissionMode,
        SafetyTier tier,
        ToolPermissionProfile? profile,
        IEventBus? eventBus = null,
        string? sessionId = null,
        long? durationMs = null,
        string? argsSummary = null)
    {
        var result = Evaluate(canonicalToolName, permissionMode, tier, profile);

        // Emit audit event (fire and forget)
        if (eventBus is not null && sessionId is not null)
        {
            var entry = ToolAuditEntry.Create(
                canonicalToolName,
                argsSummary,
                null,
                result.Decision,
                durationMs ?? 0,
                sessionId);
            _ = eventBus.PublishAsync(entry);
        }

        return result;
    }
}
