namespace JD.AI.Core.Agents;

/// <summary>
/// Controls the permission model for tool invocations within a session.
/// </summary>
public enum PermissionMode
{
    /// <summary>Default interactive mode — prompts for confirmation based on safety tiers.</summary>
    Normal,

    /// <summary>Read-only mode — only AutoApprove-tier tools (read/explore) are allowed;
    /// all write and shell tools are blocked at the filter level.</summary>
    Plan,

    /// <summary>Auto-approve file edits — ConfirmOnce tools (file writes, git ops) are
    /// auto-approved, but AlwaysConfirm tools (shell, web search) still require confirmation.</summary>
    AcceptEdits,

    /// <summary>Auto-approve everything — equivalent to --dangerously-skip-permissions.</summary>
    BypassAll,
}
