using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;

namespace JD.AI.Tests.Governance.Audit;

public sealed class AuditEventModelTests
{
    // ── AuditSeverity enum ───────────────────────────────────────────────

    [Theory]
    [InlineData(AuditSeverity.Debug, 0)]
    [InlineData(AuditSeverity.Info, 1)]
    [InlineData(AuditSeverity.Warning, 2)]
    [InlineData(AuditSeverity.Error, 3)]
    [InlineData(AuditSeverity.Critical, 4)]
    public void AuditSeverity_Values(AuditSeverity severity, int expected) =>
        ((int)severity).Should().Be(expected);

    // ── AuditEvent defaults ──────────────────────────────────────────────

    [Fact]
    public void AuditEvent_DefaultEventId_IsNonEmpty32HexChars()
    {
        var evt = new AuditEvent();
        evt.EventId.Should().NotBeNullOrWhiteSpace();
        evt.EventId.Should().HaveLength(32);
        evt.EventId.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void AuditEvent_TwoInstances_HaveDifferentEventIds()
    {
        var a = new AuditEvent();
        var b = new AuditEvent();
        a.EventId.Should().NotBe(b.EventId);
    }

    [Fact]
    public void AuditEvent_DefaultTimestamp_IsCloseToNow()
    {
        var evt = new AuditEvent();
        evt.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AuditEvent_DefaultSeverity_IsInfo()
    {
        var evt = new AuditEvent();
        evt.Severity.Should().Be(AuditSeverity.Info);
    }

    [Fact]
    public void AuditEvent_DefaultAction_IsEmpty()
    {
        var evt = new AuditEvent();
        evt.Action.Should().Be(string.Empty);
    }

    [Fact]
    public void AuditEvent_NullableProperties_DefaultToNull()
    {
        var evt = new AuditEvent();
        evt.UserId.Should().BeNull();
        evt.SessionId.Should().BeNull();
        evt.TraceId.Should().BeNull();
        evt.Resource.Should().BeNull();
        evt.Detail.Should().BeNull();
        evt.PolicyResult.Should().BeNull();
        evt.ToolName.Should().BeNull();
        evt.ToolArguments.Should().BeNull();
        evt.ToolResult.Should().BeNull();
        evt.DurationMs.Should().BeNull();
        evt.PreviousHash.Should().BeNull();
        evt.TenantId.Should().BeNull();
    }

    [Fact]
    public void AuditEvent_AllPropertiesSet()
    {
        var evt = new AuditEvent
        {
            UserId = "user-1",
            SessionId = "sess-1",
            TraceId = "trace-1",
            Action = "tool.invoke",
            Resource = "file.read",
            Detail = "Read /etc/hosts",
            Severity = AuditSeverity.Warning,
            PolicyResult = PolicyDecision.Allow,
            ToolName = "FileTools",
            ToolArguments = "{\"path\":\"/etc/hosts\"}",
            ToolResult = "contents...",
            DurationMs = 42,
            PreviousHash = "abc123",
            TenantId = "tenant-1",
        };

        evt.Action.Should().Be("tool.invoke");
        evt.Severity.Should().Be(AuditSeverity.Warning);
        evt.PolicyResult.Should().Be(PolicyDecision.Allow);
        evt.ToolName.Should().Be("FileTools");
        evt.DurationMs.Should().Be(42);
        evt.TenantId.Should().Be("tenant-1");
    }
}
