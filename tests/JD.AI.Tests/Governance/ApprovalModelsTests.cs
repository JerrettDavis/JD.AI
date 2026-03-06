using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class ApprovalModelsTests
{
    // ── ApprovalRequest record ──────────────────────────────────────────

    [Fact]
    public void ApprovalRequest_RequiredProperties()
    {
        var req = new ApprovalRequest("req-1", "Run dangerous tool");
        req.Id.Should().Be("req-1");
        req.Description.Should().Be("Run dangerous tool");
        req.Details.Should().BeNull();
        req.Kind.Should().Be(ApprovalKind.Workflow);
        req.WorkflowName.Should().BeNull();
        req.ToolName.Should().BeNull();
        req.UserId.Should().BeNull();
    }

    [Fact]
    public void ApprovalRequest_AllOptionalProperties()
    {
        var req = new ApprovalRequest(
            "req-2", "Access external DB",
            Details: "Connecting to prod read-replica",
            Kind: ApprovalKind.DataAccess,
            WorkflowName: "data-sync",
            ToolName: "db_query",
            UserId: "user-55");
        req.Details.Should().Be("Connecting to prod read-replica");
        req.Kind.Should().Be(ApprovalKind.DataAccess);
        req.WorkflowName.Should().Be("data-sync");
        req.ToolName.Should().Be("db_query");
        req.UserId.Should().Be("user-55");
    }

    [Fact]
    public void ApprovalRequest_RecordEquality()
    {
        var a = new ApprovalRequest("x", "desc");
        var b = new ApprovalRequest("x", "desc");
        a.Should().Be(b);
    }

    [Fact]
    public void ApprovalRequest_RecordInequality_DifferentKind()
    {
        var a = new ApprovalRequest("x", "desc", Kind: ApprovalKind.ToolCall);
        var b = new ApprovalRequest("x", "desc", Kind: ApprovalKind.FileSystem);
        a.Should().NotBe(b);
    }

    // ── ApprovalKind enum ───────────────────────────────────────────────

    [Theory]
    [InlineData(ApprovalKind.Workflow, 0)]
    [InlineData(ApprovalKind.ToolCall, 1)]
    [InlineData(ApprovalKind.DataAccess, 2)]
    [InlineData(ApprovalKind.ExternalRequest, 3)]
    [InlineData(ApprovalKind.FileSystem, 4)]
    public void ApprovalKind_HasExpectedValues(ApprovalKind kind, int expected) =>
        ((int)kind).Should().Be(expected);

    // ── ApprovalDecision enum ───────────────────────────────────────────

    [Theory]
    [InlineData(ApprovalDecision.Approved, 0)]
    [InlineData(ApprovalDecision.Rejected, 1)]
    [InlineData(ApprovalDecision.Timeout, 2)]
    public void ApprovalDecision_HasExpectedValues(ApprovalDecision decision, int expected) =>
        ((int)decision).Should().Be(expected);

    // ── ApprovalResult record ───────────────────────────────────────────

    [Fact]
    public void ApprovalResult_Approved_Factory()
    {
        var result = ApprovalResult.Approved();
        result.Decision.Should().Be(ApprovalDecision.Approved);
        result.Reason.Should().BeNull();
        result.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void ApprovalResult_Rejected_Factory()
    {
        var result = ApprovalResult.Rejected("too risky");
        result.Decision.Should().Be(ApprovalDecision.Rejected);
        result.Reason.Should().Be("too risky");
        result.IsApproved.Should().BeFalse();
    }

    [Fact]
    public void ApprovalResult_TimedOut_Factory()
    {
        var result = ApprovalResult.TimedOut();
        result.Decision.Should().Be(ApprovalDecision.Timeout);
        result.Reason.Should().BeNull();
        result.IsApproved.Should().BeFalse();
    }

    [Fact]
    public void ApprovalResult_RecordEquality()
    {
        var a = ApprovalResult.Approved();
        var b = ApprovalResult.Approved();
        a.Should().Be(b);
    }

    [Fact]
    public void ApprovalResult_DirectConstruction()
    {
        var result = new ApprovalResult(ApprovalDecision.Rejected, "custom reason");
        result.Decision.Should().Be(ApprovalDecision.Rejected);
        result.Reason.Should().Be("custom reason");
        result.IsApproved.Should().BeFalse();
    }
}
