using FluentAssertions;
using JD.AI.Core.Questions;
using JD.AI.Rendering;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Tests for <see cref="QuestionnaireSession"/> validation helpers.
/// </summary>
public sealed class QuestionnaireSessionTests
{
    // ── ValidateText ────────────────────────────────────────────────

    [Fact]
    public void ValidateText_NullValidation_ReturnsNull()
    {
        QuestionnaireSession.ValidateText(null, "anything").Should().BeNull();
    }

    [Fact]
    public void ValidateText_NoRules_ReturnsNull()
    {
        var v = new QuestionValidation();
        QuestionnaireSession.ValidateText(v, "anything").Should().BeNull();
    }

    [Fact]
    public void ValidateText_WithinMaxLength_ReturnsNull()
    {
        var v = new QuestionValidation { MaxLength = 10 };
        QuestionnaireSession.ValidateText(v, "short").Should().BeNull();
    }

    [Fact]
    public void ValidateText_ExceedsMaxLength_ReturnsError()
    {
        var v = new QuestionValidation { MaxLength = 5 };
        QuestionnaireSession.ValidateText(v, "too long text").Should().NotBeNull();
    }

    [Fact]
    public void ValidateText_ExceedsMaxLength_CustomMessage()
    {
        var v = new QuestionValidation { MaxLength = 5, ErrorMessage = "Too long!" };
        QuestionnaireSession.ValidateText(v, "too long text").Should().Be("Too long!");
    }

    [Fact]
    public void ValidateText_MatchesPattern_ReturnsNull()
    {
        var v = new QuestionValidation { Pattern = @"^\d+$" };
        QuestionnaireSession.ValidateText(v, "12345").Should().BeNull();
    }

    [Fact]
    public void ValidateText_DoesNotMatchPattern_ReturnsError()
    {
        var v = new QuestionValidation { Pattern = @"^\d+$" };
        QuestionnaireSession.ValidateText(v, "abc").Should().NotBeNull();
    }

    [Fact]
    public void ValidateText_DoesNotMatchPattern_CustomMessage()
    {
        var v = new QuestionValidation { Pattern = @"^\d+$", ErrorMessage = "Numbers only!" };
        QuestionnaireSession.ValidateText(v, "abc").Should().Be("Numbers only!");
    }

    [Fact]
    public void ValidateText_ExactlyMaxLength_ReturnsNull()
    {
        var v = new QuestionValidation { MaxLength = 5 };
        QuestionnaireSession.ValidateText(v, "12345").Should().BeNull();
    }

    // ── ValidateNumber ──────────────────────────────────────────────

    [Fact]
    public void ValidateNumber_NullValidation_ReturnsNull()
    {
        QuestionnaireSession.ValidateNumber(null, 42).Should().BeNull();
    }

    [Fact]
    public void ValidateNumber_NoRules_ReturnsNull()
    {
        var v = new QuestionValidation();
        QuestionnaireSession.ValidateNumber(v, 42).Should().BeNull();
    }

    [Fact]
    public void ValidateNumber_WithinRange_ReturnsNull()
    {
        var v = new QuestionValidation { Min = 1, Max = 100 };
        QuestionnaireSession.ValidateNumber(v, 50).Should().BeNull();
    }

    [Fact]
    public void ValidateNumber_BelowMin_ReturnsError()
    {
        var v = new QuestionValidation { Min = 10 };
        QuestionnaireSession.ValidateNumber(v, 5).Should().NotBeNull();
    }

    [Fact]
    public void ValidateNumber_AboveMax_ReturnsError()
    {
        var v = new QuestionValidation { Max = 100 };
        QuestionnaireSession.ValidateNumber(v, 150).Should().NotBeNull();
    }

    [Fact]
    public void ValidateNumber_BelowMin_CustomMessage()
    {
        var v = new QuestionValidation { Min = 10, ErrorMessage = "Too small!" };
        QuestionnaireSession.ValidateNumber(v, 5).Should().Be("Too small!");
    }

    [Fact]
    public void ValidateNumber_AboveMax_CustomMessage()
    {
        var v = new QuestionValidation { Max = 100, ErrorMessage = "Too big!" };
        QuestionnaireSession.ValidateNumber(v, 150).Should().Be("Too big!");
    }

    [Fact]
    public void ValidateNumber_ExactlyMin_ReturnsNull()
    {
        var v = new QuestionValidation { Min = 10 };
        QuestionnaireSession.ValidateNumber(v, 10).Should().BeNull();
    }

    [Fact]
    public void ValidateNumber_ExactlyMax_ReturnsNull()
    {
        var v = new QuestionValidation { Max = 100 };
        QuestionnaireSession.ValidateNumber(v, 100).Should().BeNull();
    }
}
