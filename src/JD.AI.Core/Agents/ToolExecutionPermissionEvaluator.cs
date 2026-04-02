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
    private static readonly HashSet<string> AcceptEditsAutoApproveTools =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "write_file",
            "edit_file",
            "batch_edit_files",
            "apply_patch",
            "git_commit",
            "git_push",
            "git_pull",
            "git_checkout",
            "git_stash",
        };

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

        if (permissionMode == PermissionMode.BypassAll)
            return new ToolExecutionGateResult(ToolExecutionGateDecision.AllowWithoutPrompt);

        // AutoApprove-tier tools bypass prompts regardless of permission mode.
        // The Plan-mode block below only applies to AlwaysConfirm/ConfirmOnce tools.
        if (tier == SafetyTier.AutoApprove)
            return new ToolExecutionGateResult(ToolExecutionGateDecision.AllowWithoutPrompt);

        if (permissionMode == PermissionMode.AcceptEdits &&
            AcceptEditsAutoApproveTools.Contains(canonicalToolName))
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

        PublishAuditDecision(canonicalToolName, result.Decision, eventBus, sessionId, durationMs, argsSummary);

        return result;
    }

    public static void PublishAuditDecision(
        string canonicalToolName,
        ToolExecutionGateDecision decision,
        IEventBus? eventBus = null,
        string? sessionId = null,
        long? durationMs = null,
        string? argsSummary = null)
    {
        if (eventBus is null || sessionId is null)
            return;

        var entry = ToolAuditEntry.Create(
            canonicalToolName,
            argsSummary,
            null,
            decision,
            durationMs ?? 0,
            sessionId);
        _ = eventBus.PublishAsync(entry);
    }
}
