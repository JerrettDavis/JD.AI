namespace JD.AI.Core.Governance;

/// <summary>
/// Provides human-in-the-loop or automated approval decisions for sensitive operations.
/// </summary>
/// <remarks>
/// Implementations include:
/// <list type="bullet">
///   <item><see cref="AutoApproveService"/> — always approves (automation / test use)</item>
///   <item><see cref="AutoRejectService"/> — always rejects (locked-down environments)</item>
///   <item><see cref="PolicyBasedApprovalService"/> — delegates to governance policy</item>
/// </list>
/// Wire this into <see cref="JD.AI.Core.Agents.AgentSession.ApprovalService"/> at startup.
/// </remarks>
public interface IApprovalService
{
    /// <summary>
    /// Requests approval for the described operation.
    /// </summary>
    /// <param name="request">Details of the operation requiring approval.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="ApprovalResult"/> indicating whether to proceed.</returns>
    Task<ApprovalResult> RequestApprovalAsync(ApprovalRequest request, CancellationToken ct = default);
}
