using FluentAssertions;
using JD.AI.Core.Tools;

namespace JD.AI.Tests.Tools;

public sealed class OpenClawEnvelopeTests
{
    // ── noContext ───────────────────────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_NoContext_ReturnsHiddenMessage()
    {
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            "full result text", summary: false, maxResultChars: null,
            noContext: true, noStream: false);

        result.Should().Be("Result hidden from context because noContext=true.");
    }

    [Fact]
    public void ApplyEnvelope_NoContext_IgnoresOtherFlags()
    {
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            "full result text", summary: true, maxResultChars: 5,
            noContext: true, noStream: true);

        result.Should().Be("Result hidden from context because noContext=true.");
    }

    // ── passthrough ────────────────────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_NoFlags_ReturnsRawResult()
    {
        const string InputText = "hello world";
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            InputText, summary: false, maxResultChars: null,
            noContext: false, noStream: false);

        result.Should().Be(InputText);
    }

    [Fact]
    public void ApplyEnvelope_NullInput_ReturnsEmpty()
    {
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            null!, summary: false, maxResultChars: null,
            noContext: false, noStream: false);

        result.Should().BeEmpty();
    }

    // ── noStream is a no-op ────────────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_NoStream_DoesNotAffectOutput()
    {
        const string InputText = "some text";
        var withStream = OpenClawCompatibilityTools.ApplyEnvelope(
            InputText, false, null, false, noStream: true);
        var withoutStream = OpenClawCompatibilityTools.ApplyEnvelope(
            InputText, false, null, false, noStream: false);

        withStream.Should().Be(withoutStream);
    }

    // ── maxResultChars truncation ──────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_MaxResultChars_TruncatesLongOutput()
    {
        var inputText = new string('x', 100);
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, false, maxResultChars: 20, noContext: false, noStream: false);

        result.Should().StartWith(new string('x', 20));
        result.Should().Contain("[truncated, 80 more chars]");
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_ExactLength_NoTruncation()
    {
        var inputText = new string('a', 50);
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, false, maxResultChars: 50, noContext: false, noStream: false);

        result.Should().Be(inputText);
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_ShorterThanLimit_NoTruncation()
    {
        const string InputText = "short";
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            InputText, false, maxResultChars: 1000, noContext: false, noStream: false);

        result.Should().Be(InputText);
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_ZeroOrNegative_NoTruncation()
    {
        var inputText = new string('z', 200);
        var resultZero = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, false, maxResultChars: 0, noContext: false, noStream: false);
        var resultNeg = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, false, maxResultChars: -5, noContext: false, noStream: false);

        resultZero.Should().Be(inputText);
        resultNeg.Should().Be(inputText);
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_Null_NoTruncation()
    {
        var inputText = new string('m', 500);
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, false, maxResultChars: null, noContext: false, noStream: false);

        result.Should().Be(inputText);
    }

    // ── summary (Summarize) ────────────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_Summary_ReducesToEightLines()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i}");
        var inputText = string.Join("\n", lines);

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        var resultLines = result.Split(Environment.NewLine, StringSplitOptions.None);
        resultLines.Should().HaveCountLessThanOrEqualTo(8);
    }

    [Fact]
    public void ApplyEnvelope_Summary_FewerThanEightLines_NoTruncation()
    {
        var inputText = "Line1\nLine2\nLine3";

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        var resultLines = result.Split(Environment.NewLine, StringSplitOptions.None);
        resultLines.Should().HaveCount(3);
    }

    [Fact]
    public void ApplyEnvelope_Summary_BlankLines_AreStripped()
    {
        var inputText = "First\n\n\nSecond\n   \nThird";

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        result.Should().Contain("First");
        result.Should().Contain("Second");
        result.Should().Contain("Third");
        // Blank lines should be filtered
        var resultLines = result.Split(Environment.NewLine, StringSplitOptions.None);
        resultLines.All(l => !string.IsNullOrWhiteSpace(l)).Should().BeTrue();
    }

    [Fact]
    public void ApplyEnvelope_Summary_EmptyInput_ReturnsEmpty()
    {
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            "", summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ApplyEnvelope_Summary_WhitespaceOnly_ReturnsInput()
    {
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            "   ", summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        result.Should().Be("   ");
    }

    [Fact]
    public void ApplyEnvelope_Summary_AllBlankLines_ReturnsTruncatedInput()
    {
        // All lines are whitespace — Summarize produces 0 non-blank lines
        // and falls back to truncation at 240 chars
        var inputText = "\n\n\n\n";
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        // Short input, returned as-is (under 240 chars)
        result.Should().Be(inputText);
    }

    // ── summary + maxResultChars combined ──────────────────────────────

    [Fact]
    public void ApplyEnvelope_SummaryThenTruncate_AppliesBoth()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i} with some content");
        var inputText = string.Join("\n", lines);

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            inputText, summary: true, maxResultChars: 30,
            noContext: false, noStream: false);

        // Summary first reduces lines, then maxResultChars truncates
        result.Should().Contain("[truncated,");
    }
}
