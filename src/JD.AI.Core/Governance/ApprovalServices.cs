namespace JD.AI.Core.Governance;

/// <summary>
/// <see cref="IApprovalService"/> that always approves requests without prompting.
/// Suitable for fully automated pipelines or integration tests where human interaction
/// is not available.
/// </summary>
public sealed class AutoApproveService : IApprovalService
{
    /// <inheritdoc />
    public Task<ApprovalResult> RequestApprovalAsync(ApprovalRequest request, CancellationToken ct = default)
        => Task.FromResult(ApprovalResult.Approved());
}

/// <summary>
/// <see cref="IApprovalService"/> that always rejects requests.
/// Use in locked-down environments where no operation may proceed without
/// replacing this service with a human-in-the-loop implementation.
/// </summary>
public sealed class AutoRejectService : IApprovalService
{
    private readonly string _reason;

    /// <param name="reason">Rejection reason included in every <see cref="ApprovalResult"/>.</param>
    public AutoRejectService(string reason = "Approval is required but no approval service is configured.")
        => _reason = reason;

    /// <inheritdoc />
    public Task<ApprovalResult> RequestApprovalAsync(ApprovalRequest request, CancellationToken ct = default)
        => Task.FromResult(ApprovalResult.Rejected(_reason));
}

/// <summary>
/// <see cref="IApprovalService"/> that consults a <see cref="PolicySpec"/> to determine
/// whether a kind of operation requires approval, and delegates to an inner service
/// when it does.
/// </summary>
public sealed class PolicyBasedApprovalService : IApprovalService
{
    private readonly PolicySpec _policy;
    private readonly IApprovalService _inner;

    public PolicyBasedApprovalService(PolicySpec policy, IApprovalService inner)
    {
        _policy = policy;
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<ApprovalResult> RequestApprovalAsync(ApprovalRequest request, CancellationToken ct = default)
    {
        // If the policy requires workflow approval gates, delegate to the inner service
        if (_policy.Workflows is { } wf && wf.RequireApprovalGate)
            return await _inner.RequestApprovalAsync(request, ct).ConfigureAwait(false);

        // Tools: check if tool requires approval based on policy denied/allowed lists
        if (request.Kind == ApprovalKind.ToolCall && request.ToolName is not null)
        {
            var toolPolicy = _policy.Tools;
            if (toolPolicy?.RequireApprovalFor?.Contains(request.ToolName, StringComparer.OrdinalIgnoreCase) == true)
                return await _inner.RequestApprovalAsync(request, ct).ConfigureAwait(false);
        }

        return ApprovalResult.Approved();
    }
}
