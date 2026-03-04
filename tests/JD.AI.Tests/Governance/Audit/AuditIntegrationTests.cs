using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using NSubstitute;

namespace JD.AI.Tests.Governance.Audit;

public class AuditIntegrationTests
{
    [Fact]
    public async Task AuditService_WithMultipleSinks_DispatchesToAll()
    {
        var sink1 = Substitute.For<IAuditSink>();
        sink1.Name.Returns("sink1");
        var sink2 = Substitute.For<IAuditSink>();
        sink2.Name.Returns("sink2");

        var service = new AuditService([sink1, sink2]);

        var evt = new AuditEvent
        {
            Action = "tool.invoke",
            Resource = "read_file",
            SessionId = "test-session",
            Severity = AuditSeverity.Info,
        };

        await service.EmitAsync(evt);

        await sink1.Received(1).WriteAsync(evt, Arg.Any<CancellationToken>());
        await sink2.Received(1).WriteAsync(evt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditEvent_WithPolicyDeny_HasWarningSeverity()
    {
        var sink = Substitute.For<IAuditSink>();
        sink.Name.Returns("test");
        var service = new AuditService([sink]);

        var evt = new AuditEvent
        {
            Action = "tool.invoke",
            Resource = "run_command",
            PolicyResult = PolicyDecision.Deny,
            Severity = AuditSeverity.Warning,
        };

        await service.EmitAsync(evt);

        await sink.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(e =>
                e.PolicyResult == PolicyDecision.Deny &&
                e.Severity == AuditSeverity.Warning),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditEvent_SessionLifecycle_EmitsCorrectActions()
    {
        var sink = Substitute.For<IAuditSink>();
        sink.Name.Returns("test");
        var service = new AuditService([sink]);

        var createEvt = new AuditEvent
        {
            Action = "session.create",
            SessionId = "sess-123",
            Resource = "/path/to/project",
            Severity = AuditSeverity.Info,
        };

        var closeEvt = new AuditEvent
        {
            Action = "session.close",
            SessionId = "sess-123",
            Resource = "/path/to/project",
            Detail = "turns=5; tokens=1000",
            Severity = AuditSeverity.Info,
        };

        await service.EmitAsync(createEvt);
        await service.EmitAsync(closeEvt);

        await sink.Received(2).WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>());
        await sink.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(e => e.Action == "session.create"),
            Arg.Any<CancellationToken>());
        await sink.Received(1).WriteAsync(
            Arg.Is<AuditEvent>(e => e.Action == "session.close"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PolicyEvaluator_DeniedTool_ReturnsCorrectDecision()
    {
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy
            {
                Denied = ["run_command", "execute_code"],
            },
        };

        var evaluator = new PolicyEvaluator(policy);

        var result = evaluator.EvaluateTool("run_command", new PolicyContext());
        result.Decision.Should().Be(PolicyDecision.Deny);
        result.Reason.Should().Contain("denied");

        var allowed = evaluator.EvaluateTool("read_file", new PolicyContext());
        allowed.Decision.Should().Be(PolicyDecision.Allow);
    }

    [Fact]
    public void PolicyEvaluator_AllowedListRestriction_BlocksUnlisted()
    {
        var policy = new PolicySpec
        {
            Tools = new ToolPolicy
            {
                Allowed = ["read_file", "grep", "glob"],
            },
        };

        var evaluator = new PolicyEvaluator(policy);

        var allowed = evaluator.EvaluateTool("read_file", new PolicyContext());
        allowed.Decision.Should().Be(PolicyDecision.Allow);

        var denied = evaluator.EvaluateTool("run_command", new PolicyContext());
        denied.Decision.Should().Be(PolicyDecision.Deny);
    }

    [Fact]
    public void PolicyEvaluator_ProviderRestriction_EnforcesPolicy()
    {
        var policy = new PolicySpec
        {
            Providers = new ProviderPolicy
            {
                Allowed = ["ollama"],
                Denied = ["claude"],
            },
        };

        var evaluator = new PolicyEvaluator(policy);

        var ollama = evaluator.EvaluateProvider("ollama", new PolicyContext());
        ollama.Decision.Should().Be(PolicyDecision.Allow);

        var claude = evaluator.EvaluateProvider("claude", new PolicyContext());
        claude.Decision.Should().Be(PolicyDecision.Deny);
    }
}
