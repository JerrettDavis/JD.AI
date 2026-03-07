namespace JD.AI.Core.Tools;

/// <summary>
/// Defines the safety tier for a tool.
/// </summary>
public enum SafetyTier
{
    /// <summary>Execute without confirmation.</summary>
    AutoApprove,

    /// <summary>Ask once per session, then auto-approve.</summary>
    ConfirmOnce,

    /// <summary>Always ask before execution.</summary>
    AlwaysConfirm,
}
