using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class DataClassificationTests
{
    // ── ClassificationAction enum ─────────────────────────────────────────

    [Theory]
    [InlineData(ClassificationAction.Redact, 0)]
    [InlineData(ClassificationAction.RedactAndAudit, 1)]
    [InlineData(ClassificationAction.AuditOnly, 2)]
    [InlineData(ClassificationAction.DenyAndAudit, 3)]
    public void ClassificationAction_Values(ClassificationAction action, int expected) =>
        ((int)action).Should().Be(expected);

    // ── DataClassification ────────────────────────────────────────────────

    [Fact]
    public void DataClassification_Defaults()
    {
        var cls = new DataClassification();
        cls.Name.Should().BeEmpty();
        cls.Patterns.Should().BeEmpty();
        cls.Action.Should().Be(ClassificationAction.Redact);
        cls.DenyProviders.Should().BeEmpty();
    }

    [Fact]
    public void DataClassification_CustomValues()
    {
        var cls = new DataClassification
        {
            Name = "SSN",
            Patterns = [@"\d{3}-\d{2}-\d{4}"],
            Action = ClassificationAction.DenyAndAudit,
            DenyProviders = ["*"],
        };
        cls.Name.Should().Be("SSN");
        cls.Patterns.Should().HaveCount(1);
        cls.Action.Should().Be(ClassificationAction.DenyAndAudit);
        cls.DenyProviders.Should().Contain("*");
    }

    // ── ClassificationMatch ───────────────────────────────────────────────

    [Fact]
    public void ClassificationMatch_Construction()
    {
        var match = new ClassificationMatch("PII", ClassificationAction.RedactAndAudit);
        match.ClassificationName.Should().Be("PII");
        match.Action.Should().Be(ClassificationAction.RedactAndAudit);
    }

    // ── RedactionResult ───────────────────────────────────────────────────

    [Fact]
    public void RedactionResult_NoMatches()
    {
        var result = new RedactionResult("clean text", []);
        result.Content.Should().Be("clean text");
        result.HasMatches.Should().BeFalse();
        result.ShouldDeny.Should().BeFalse();
    }

    [Fact]
    public void RedactionResult_WithDenyMatch_ShouldDenyTrue()
    {
        var matches = new[]
        {
            new ClassificationMatch("PAN", ClassificationAction.DenyAndAudit),
        };
        var result = new RedactionResult("redacted", matches);
        result.ShouldDeny.Should().BeTrue();
        result.HasMatches.Should().BeTrue();
    }

    [Fact]
    public void RedactionResult_WithAuditOnlyMatch_ShouldDenyFalse()
    {
        var matches = new[]
        {
            new ClassificationMatch("Name", ClassificationAction.AuditOnly),
        };
        var result = new RedactionResult("content", matches);
        result.ShouldDeny.Should().BeFalse();
        result.HasMatches.Should().BeTrue();
    }

    [Fact]
    public void RedactionResult_MixedMatches_ShouldDenyIfAnyDeny()
    {
        var matches = new[]
        {
            new ClassificationMatch("Name", ClassificationAction.AuditOnly),
            new ClassificationMatch("SSN", ClassificationAction.DenyAndAudit),
        };
        var result = new RedactionResult("redacted", matches);
        result.ShouldDeny.Should().BeTrue();
    }
}
