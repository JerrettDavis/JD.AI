using JD.AI.Workflows.Consensus;

namespace JD.AI.Tests.Workflows;

public sealed class WorkflowConsensusTests
{
    [Fact]
    public void Propose_SingleApproval_AutoApproves()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 1);
        var proposal = sut.Propose("my-wf", "1.0", "alice");

        Assert.Equal(ProposalStatus.Approved, proposal.Status);
        Assert.Single(proposal.Approvals);
    }

    [Fact]
    public void Propose_MultipleApprovals_StaysPendingUntilThreshold()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 3);
        var proposal = sut.Propose("my-wf", "1.0", "alice");

        Assert.Equal(ProposalStatus.Pending, proposal.Status);
        Assert.Single(proposal.Approvals);
    }

    [Fact]
    public void Approve_ReachesThreshold_Approves()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 2);
        sut.Propose("my-wf", "1.0", "alice");

        var result = sut.Approve("my-wf", "1.0", "bob");

        Assert.True(result);
        var proposal = sut.GetProposal("my-wf", "1.0");
        Assert.Equal(ProposalStatus.Approved, proposal!.Status);
    }

    [Fact]
    public void Approve_DuplicateUser_ReturnsFalse()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 3);
        sut.Propose("my-wf", "1.0", "alice");

        var result = sut.Approve("my-wf", "1.0", "alice");
        Assert.False(result);
    }

    [Fact]
    public void Reject_SetsRejectedStatus()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 2);
        sut.Propose("my-wf", "1.0", "alice");

        var result = sut.Reject("my-wf", "1.0", "bob", "Needs more review");

        Assert.True(result);
        var proposal = sut.GetProposal("my-wf", "1.0");
        Assert.Equal(ProposalStatus.Rejected, proposal!.Status);
        Assert.Equal("Needs more review", proposal.RejectionReason);
    }

    [Fact]
    public void Propose_DuplicateWorkflowVersion_Throws()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 2);
        sut.Propose("my-wf", "1.0", "alice");

        Assert.Throws<InvalidOperationException>(() =>
            sut.Propose("my-wf", "1.0", "bob"));
    }

    [Fact]
    public void ListProposals_FiltersbyStatus()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 2);
        sut.Propose("wf-a", "1.0", "alice");
        sut.Propose("wf-b", "1.0", "alice");
        sut.Reject("wf-b", "1.0", "bob", "no");

        var pending = sut.ListProposals(ProposalStatus.Pending);
        Assert.Single(pending);

        var rejected = sut.ListProposals(ProposalStatus.Rejected);
        Assert.Single(rejected);
    }

    [Fact]
    public void RemoveProposal_CleansUp()
    {
        var sut = new WorkflowConsensus(requiredApprovals: 1);
        sut.Propose("wf", "1.0", "alice");

        Assert.True(sut.RemoveProposal("wf", "1.0"));
        Assert.Null(sut.GetProposal("wf", "1.0"));
    }

    [Fact]
    public void Approve_Nonexistent_ThrowsKeyNotFound()
    {
        var sut = new WorkflowConsensus();
        Assert.Throws<KeyNotFoundException>(() =>
            sut.Approve("no-such", "1.0", "alice"));
    }

    [Fact]
    public void Constructor_ZeroApprovals_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkflowConsensus(requiredApprovals: 0));
    }
}
