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
        const string raw = "hello world";
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, summary: false, maxResultChars: null,
            noContext: false, noStream: false);

        result.Should().Be(raw);
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
        const string raw = "some text";
        var withStream = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, null, false, noStream: true);
        var withoutStream = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, null, false, noStream: false);

        withStream.Should().Be(withoutStream);
    }

    // ── maxResultChars truncation ──────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_MaxResultChars_TruncatesLongOutput()
    {
        var raw = new string('x', 100);
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, maxResultChars: 20, noContext: false, noStream: false);

        result.Should().StartWith(new string('x', 20));
        result.Should().Contain("[truncated, 80 more chars]");
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_ExactLength_NoTruncation()
    {
        var raw = new string('a', 50);
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, maxResultChars: 50, noContext: false, noStream: false);

        result.Should().Be(raw);
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_ShorterThanLimit_NoTruncation()
    {
        const string raw = "short";
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, maxResultChars: 1000, noContext: false, noStream: false);

        result.Should().Be(raw);
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_ZeroOrNegative_NoTruncation()
    {
        var raw = new string('z', 200);
        var resultZero = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, maxResultChars: 0, noContext: false, noStream: false);
        var resultNeg = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, maxResultChars: -5, noContext: false, noStream: false);

        resultZero.Should().Be(raw);
        resultNeg.Should().Be(raw);
    }

    [Fact]
    public void ApplyEnvelope_MaxResultChars_Null_NoTruncation()
    {
        var raw = new string('m', 500);
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, false, maxResultChars: null, noContext: false, noStream: false);

        result.Should().Be(raw);
    }

    // ── summary (Summarize) ────────────────────────────────────────────

    [Fact]
    public void ApplyEnvelope_Summary_ReducesToEightLines()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i}");
        var raw = string.Join("\n", lines);

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        var resultLines = result.Split(Environment.NewLine, StringSplitOptions.None);
        resultLines.Should().HaveCountLessThanOrEqualTo(8);
    }

    [Fact]
    public void ApplyEnvelope_Summary_FewerThanEightLines_NoTruncation()
    {
        var raw = "Line1\nLine2\nLine3";

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        var resultLines = result.Split(Environment.NewLine, StringSplitOptions.None);
        resultLines.Should().HaveCount(3);
    }

    [Fact]
    public void ApplyEnvelope_Summary_BlankLines_AreStripped()
    {
        var raw = "First\n\n\nSecond\n   \nThird";

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, summary: true, maxResultChars: null,
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
        var raw = "\n\n\n\n";
        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, summary: true, maxResultChars: null,
            noContext: false, noStream: false);

        // Short input, returned as-is (under 240 chars)
        result.Should().Be(raw);
    }

    // ── summary + maxResultChars combined ──────────────────────────────

    [Fact]
    public void ApplyEnvelope_SummaryThenTruncate_AppliesBoth()
    {
        var lines = Enumerable.Range(1, 20).Select(i => $"Line {i} with some content");
        var raw = string.Join("\n", lines);

        var result = OpenClawCompatibilityTools.ApplyEnvelope(
            raw, summary: true, maxResultChars: 30,
            noContext: false, noStream: false);

        // Summary first reduces lines, then maxResultChars truncates
        result.Should().Contain("[truncated,");
    }
}
