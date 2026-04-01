using FluentAssertions;
using JD.AI.Core.Agents;
using JD.AI.Core.Events;
using JD.AI.Core.Tools;
using NSubstitute;

namespace JD.AI.Tests.Agents;

public sealed class ToolExecutionPermissionEvaluatorTests
{
    // ── 4-arg overload — decision branches ─────────────────────────────────

    [Fact]
    public void Evaluate_4arg_ExplicitDeny_Blocks()
    {
        var profile = new ToolPermissionProfile();
        profile.AddDenied("git_push", projectScope: false);

        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "git_push", PermissionMode.Normal, SafetyTier.AutoApprove, profile);

        result.Decision.Should().Be(ToolExecutionGateDecision.Blocked);
    }

    [Fact]
    public void Evaluate_4arg_PlanModeNonAutoApprove_Blocks()
    {
        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "read_file", PermissionMode.Plan, SafetyTier.AlwaysConfirm, null);

        result.Decision.Should().Be(ToolExecutionGateDecision.Blocked);
    }

    [Fact]
    public void Evaluate_4arg_PlanModeAutoApprove_Allows()
    {
        // AutoApprove bypasses plan-mode blocking
        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "read_file", PermissionMode.Plan, SafetyTier.AutoApprove, null);

        result.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
    }

    [Fact]
    public void Evaluate_4arg_ExplicitAllow_AllowsWithoutPrompt()
    {
        var profile = new ToolPermissionProfile();
        profile.AddAllowed("run_command", projectScope: false);

        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "run_command", PermissionMode.Normal, SafetyTier.AlwaysConfirm, profile);

        result.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
    }

    [Fact]
    public void Evaluate_4arg_NoMatch_RequiresPrompt()
    {
        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "unknown_tool", PermissionMode.Normal, SafetyTier.ConfirmOnce, null);

        result.Decision.Should().Be(ToolExecutionGateDecision.RequirePrompt);
    }

    // ── 8-arg overload — audit event emission ──────────────────────────────

    [Fact]
    public void Evaluate_8arg_PublishesAuditEvent_WhenEventBusAndSessionIdProvided()
    {
        var mockBus = Substitute.For<IEventBus>();
        var profile = new ToolPermissionProfile();

        var result = ToolExecutionPermissionEvaluator.Evaluate(
            canonicalToolName: "read_file",
            permissionMode: PermissionMode.Normal,
            tier: SafetyTier.AutoApprove,
            profile: profile,
            eventBus: mockBus,
            sessionId: "sess-123",
            durationMs: 42,
            argsSummary: "path=README.md");

        result.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
        _ = mockBus.Received(1).PublishAsync(
            Arg.Is<GatewayEvent>(e =>
                e.EventType == "tool.audit" &&
                e.SourceId == "sess-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Evaluate_8arg_SkipsAudit_WhenEventBusIsNull()
    {
        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "read_file", PermissionMode.Normal, SafetyTier.AutoApprove, null,
            eventBus: null,
            sessionId: "sess-123",
            durationMs: null,
            argsSummary: null);

        result.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
    }

    [Fact]
    public void Evaluate_8arg_SkipsAudit_WhenSessionIdIsNull()
    {
        var mockBus = Substitute.For<IEventBus>();

        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "read_file", PermissionMode.Normal, SafetyTier.AutoApprove, null,
            eventBus: mockBus,
            sessionId: null,
            durationMs: null,
            argsSummary: null);

        result.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
        _ = mockBus.Received(0).PublishAsync(
            Arg.Any<GatewayEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_8arg_MapsBlockedDecision_ToDenied()
    {
        var mockBus = Substitute.For<IEventBus>();
        var profile = new ToolPermissionProfile();
        profile.AddDenied("git_push", projectScope: false);

        var result2 = ToolExecutionPermissionEvaluator.Evaluate(
            "git_push", PermissionMode.Normal, SafetyTier.AutoApprove, profile,
            mockBus, "sess-456", 10, "force=true");

        await mockBus.Received(1).PublishAsync(
            Arg.Is<GatewayEvent>(e =>
                e.EventType == "tool.audit" &&
                e.SourceId == "sess-456"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evaluate_8arg_MapsRequirePromptDecision_ToPrompted()
    {
        var mockBus = Substitute.For<IEventBus>();

        var result3 = ToolExecutionPermissionEvaluator.Evaluate(
            "some_tool", PermissionMode.Normal, SafetyTier.ConfirmOnce, null,
            mockBus, "sess-789", 5, null);

        await mockBus.Received(1).PublishAsync(
            Arg.Any<GatewayEvent>(),
            Arg.Any<CancellationToken>());
    }

    // ── Null profile ─────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_NullProfile_TreatedAsEmptyProfile()
    {
        var result = ToolExecutionPermissionEvaluator.Evaluate(
            "anything", PermissionMode.Normal, SafetyTier.AutoApprove, profile: null);

        result.Decision.Should().Be(ToolExecutionGateDecision.AllowWithoutPrompt);
    }
}
