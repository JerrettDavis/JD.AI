using System.Collections.Concurrent;

namespace JD.AI.Workflows.Consensus;

/// <summary>
/// Consensus mechanism for publishing workflows to shared catalogs.
/// Implements a simple approval voting protocol: a workflow version must receive
/// the required number of approvals before it can be published.
/// </summary>
public sealed class WorkflowConsensus
{
    private readonly ConcurrentDictionary<string, PublishProposal> _proposals = new(StringComparer.Ordinal);
    private readonly int _requiredApprovals;

    /// <summary>
    /// Creates a new consensus instance.
    /// </summary>
    /// <param name="requiredApprovals">
    /// Minimum number of distinct approvals needed to publish. Defaults to 1 (single-user mode).
    /// </param>
    public WorkflowConsensus(int requiredApprovals = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredApprovals, 1);
        _requiredApprovals = requiredApprovals;
    }

    /// <summary>Number of active proposals.</summary>
    public int ProposalCount => _proposals.Count;

    /// <summary>Creates a proposal to publish a workflow version.</summary>
    public PublishProposal Propose(string workflowName, string version, string proposer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposer);

        var key = MakeKey(workflowName, version);
        var proposal = new PublishProposal
        {
            ProposalId = Guid.NewGuid().ToString("N")[..16],
            WorkflowName = workflowName,
            Version = version,
            Proposer = proposer,
            CreatedAt = DateTimeOffset.UtcNow,
            RequiredApprovals = _requiredApprovals,
            Status = ProposalStatus.Pending,
        };

        if (!_proposals.TryAdd(key, proposal))
            throw new InvalidOperationException(
                $"A proposal for '{workflowName}' v{version} already exists");

        // The proposer's proposal counts as the first approval
        proposal.Approvals.Add(new Approval(proposer, DateTimeOffset.UtcNow));
        CheckThreshold(proposal);

        return proposal;
    }

    /// <summary>Approves a pending proposal. Each user can only approve once.</summary>
    public bool Approve(string workflowName, string version, string approver)
    {
        var key = MakeKey(workflowName, version);
        if (!_proposals.TryGetValue(key, out var proposal))
            throw new KeyNotFoundException($"No proposal found for '{workflowName}' v{version}");

        if (proposal.Status != ProposalStatus.Pending)
            return false;

        if (proposal.Approvals.Any(a =>
                string.Equals(a.User, approver, StringComparison.OrdinalIgnoreCase)))
            return false; // Already approved

        proposal.Approvals.Add(new Approval(approver, DateTimeOffset.UtcNow));
        CheckThreshold(proposal);

        return true;
    }

    /// <summary>Rejects a pending proposal with a reason.</summary>
    public bool Reject(string workflowName, string version, string rejector, string reason)
    {
        var key = MakeKey(workflowName, version);
        if (!_proposals.TryGetValue(key, out var proposal))
            throw new KeyNotFoundException($"No proposal found for '{workflowName}' v{version}");

        if (proposal.Status != ProposalStatus.Pending)
            return false;

        proposal.Status = ProposalStatus.Rejected;
        proposal.ResolvedAt = DateTimeOffset.UtcNow;
        proposal.RejectionReason = reason;
        proposal.RejectedBy = rejector;

        return true;
    }

    /// <summary>Gets the current proposal for a workflow version.</summary>
    public PublishProposal? GetProposal(string workflowName, string version)
    {
        var key = MakeKey(workflowName, version);
        return _proposals.TryGetValue(key, out var proposal) ? proposal : null;
    }

    /// <summary>Lists all proposals with an optional status filter.</summary>
    public IReadOnlyList<PublishProposal> ListProposals(ProposalStatus? status = null) =>
        _proposals.Values
            .Where(p => !status.HasValue || p.Status == status.Value)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

    /// <summary>Removes a resolved (approved/rejected) proposal.</summary>
    public bool RemoveProposal(string workflowName, string version)
    {
        var key = MakeKey(workflowName, version);
        return _proposals.TryRemove(key, out _);
    }

    private void CheckThreshold(PublishProposal proposal)
    {
        if (proposal.Approvals.Count >= _requiredApprovals)
        {
            proposal.Status = ProposalStatus.Approved;
            proposal.ResolvedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string MakeKey(string name, string version) => $"{name}::{version}";
}

/// <summary>A proposal to publish a workflow version.</summary>
public sealed class PublishProposal
{
    public string ProposalId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Proposer { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int RequiredApprovals { get; init; }
    public ProposalStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public string? RejectedBy { get; set; }
    public IList<Approval> Approvals { get; init; } = [];
}

/// <summary>An individual approval vote.</summary>
public sealed record Approval(string User, DateTimeOffset At);

/// <summary>Status of a publish proposal.</summary>
public enum ProposalStatus
{
    Pending,
    Approved,
    Rejected,
}
