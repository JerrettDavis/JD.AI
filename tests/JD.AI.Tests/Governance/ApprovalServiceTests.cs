using JD.AI.Core.Governance;
using Xunit;

namespace JD.AI.Tests.Governance;

public sealed class ApprovalServiceTests
{
    // ── AutoApproveService ─────────────────────────────────────────────────

    [Fact]
    public async Task AutoApprove_AlwaysApproves()
    {
        var service = new AutoApproveService();
        var result = await service.RequestApprovalAsync(MakeRequest());
        Assert.True(result.IsApproved);
        Assert.Equal(ApprovalDecision.Approved, result.Decision);
    }

    // ── AutoRejectService ──────────────────────────────────────────────────

    [Fact]
    public async Task AutoReject_AlwaysRejects()
    {
        var service = new AutoRejectService("no approvals allowed");
        var result = await service.RequestApprovalAsync(MakeRequest());
        Assert.False(result.IsApproved);
        Assert.Equal(ApprovalDecision.Rejected, result.Decision);
        Assert.Equal("no approvals allowed", result.Reason);
    }

    [Fact]
    public async Task AutoReject_DefaultReason()
    {
        var service = new AutoRejectService();
        var result = await service.RequestApprovalAsync(MakeRequest());
        Assert.False(result.IsApproved);
        Assert.NotEmpty(result.Reason!);
    }

    // ── ApprovalResult static factories ───────────────────────────────────

    [Fact]
    public void ApprovalResult_Approved_IsApproved()
    {
        var result = ApprovalResult.Approved();
        Assert.True(result.IsApproved);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void ApprovalResult_Rejected_IsRejected()
    {
        var result = ApprovalResult.Rejected("test reason");
        Assert.False(result.IsApproved);
        Assert.Equal("test reason", result.Reason);
    }

    [Fact]
    public void ApprovalResult_TimedOut_IsNotApproved()
    {
        var result = ApprovalResult.TimedOut();
        Assert.False(result.IsApproved);
        Assert.Equal(ApprovalDecision.Timeout, result.Decision);
    }

    // ── PolicyBasedApprovalService ─────────────────────────────────────────

    [Fact]
    public async Task PolicyBased_NoPolicy_AutoApproves()
    {
        var policy = new PolicySpec(); // no workflow, no tool policy
        var inner = new AutoRejectService("should not be called");
        var service = new PolicyBasedApprovalService(policy, inner);

        var result = await service.RequestApprovalAsync(MakeRequest(ApprovalKind.Workflow));
        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task PolicyBased_RequireApprovalGate_DelegatesToInner()
    {
        var policy = new PolicySpec
        {
            Workflows = new WorkflowPolicy { RequireApprovalGate = true }
        };
        var inner = new AutoRejectService("policy gate rejected");
        var service = new PolicyBasedApprovalService(policy, inner);

        var result = await service.RequestApprovalAsync(MakeRequest(ApprovalKind.Workflow));
        Assert.False(result.IsApproved);
        Assert.Equal("policy gate rejected", result.Reason);
    }

    [Fact]
    public async Task PolicyBased_RequireApprovalGate_DoesNotAffectToolCalls()
    {
        var policy = new PolicySpec
        {
            Workflows = new WorkflowPolicy { RequireApprovalGate = true }
        };
        var inner = new AutoRejectService("should not be called");
        var service = new PolicyBasedApprovalService(policy, inner);

        // WorkflowPolicy gate must not affect tool-call approval requests
        var result = await service.RequestApprovalAsync(
            new ApprovalRequest("abc", "safe tool", Kind: ApprovalKind.ToolCall, ToolName: "safe_tool"));
        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task PolicyBased_ToolInRequireApprovalFor_DelegatesToInner()
    {
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy { RequireApprovalFor = ["dangerous_tool"] }
        };
        var inner = new AutoRejectService("tool needs approval");
        var service = new PolicyBasedApprovalService(policy, inner);

        var request = new ApprovalRequest(
            Id: "abc",
            Description: "call dangerous tool",
            Kind: ApprovalKind.ToolCall,
            ToolName: "dangerous_tool");

        var result = await service.RequestApprovalAsync(request);
        Assert.False(result.IsApproved);
    }

    [Fact]
    public async Task PolicyBased_ToolNotInRequireApprovalFor_AutoApproves()
    {
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy { RequireApprovalFor = ["dangerous_tool"] }
        };
        var inner = new AutoRejectService("should not be reached");
        var service = new PolicyBasedApprovalService(policy, inner);

        var request = new ApprovalRequest(
            Id: "abc",
            Description: "call safe tool",
            Kind: ApprovalKind.ToolCall,
            ToolName: "safe_tool");

        var result = await service.RequestApprovalAsync(request);
        Assert.True(result.IsApproved);
    }

    [Fact]
    public async Task PolicyBased_ToolRequireApprovalFor_CaseInsensitive()
    {
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy { RequireApprovalFor = ["DangerousTool"] }
        };
        var inner = new AutoRejectService("rejected");
        var service = new PolicyBasedApprovalService(policy, inner);

        var request = new ApprovalRequest(
            Id: "abc",
            Description: "test",
            Kind: ApprovalKind.ToolCall,
            ToolName: "dangeroustool");

        var result = await service.RequestApprovalAsync(request);
        Assert.False(result.IsApproved);
    }

    // ── PolicyResolver merges RequireApprovalGate + RequireApprovalFor ────

    [Fact]
    public void PolicyResolver_MergesRequireApprovalGate_AnyWins()
    {
        var docs = new[]
        {
            MakeDoc(new PolicySpec { Workflows = new WorkflowPolicy { RequireApprovalGate = false } }),
            MakeDoc(new PolicySpec { Workflows = new WorkflowPolicy { RequireApprovalGate = true } }),
        };
        var resolved = PolicyResolver.Resolve(docs);
        Assert.True(resolved.Workflows?.RequireApprovalGate);
    }

    [Fact]
    public void PolicyResolver_MergesRequireApprovalFor_Union()
    {
        var docs = new[]
        {
            MakeDoc(new PolicySpec { Tools = new ToolPolicy { RequireApprovalFor = ["tool_a"] } }),
            MakeDoc(new PolicySpec { Tools = new ToolPolicy { RequireApprovalFor = ["tool_b"] } }),
        };
        var resolved = PolicyResolver.Resolve(docs);
        Assert.Contains("tool_a", resolved.Tools?.RequireApprovalFor ?? []);
        Assert.Contains("tool_b", resolved.Tools?.RequireApprovalFor ?? []);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ApprovalRequest MakeRequest(ApprovalKind kind = ApprovalKind.Workflow) =>
        new("test-id", "test approval", Kind: kind);

    private static PolicyDocument MakeDoc(PolicySpec spec) => new()
    {
        Metadata = new PolicyMetadata { Scope = PolicyScope.User, Priority = 10 },
        Spec = spec,
    };
}
