using FluentAssertions;
using JD.AI.Rendering;
using Xunit;

namespace JD.AI.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="FooterTemplate"/> and <see cref="TemplateToken"/>.
/// </summary>
public sealed class FooterTemplateTests
{
    // ── Parse ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsSegmentTokens_Correctly()
    {
        var template = FooterTemplate.Parse("{model} │ {mode}");

        var segmentKeys = template.Tokens
            .Where(t => !t.IsLiteral)
            .Select(t => t.SegmentKey)
            .ToList();

        segmentKeys.Should().BeEquivalentTo(["model", "mode"]);
    }

    [Fact]
    public void Parse_MarksConditionalSegments_AsOptional()
    {
        var template = FooterTemplate.Parse("{model} │ {session?} │ {mode}");

        var conditionalTokens = template.Tokens.Where(t => !t.IsLiteral && t.IsConditional).ToList();
        var requiredTokens = template.Tokens.Where(t => !t.IsLiteral && !t.IsConditional).ToList();

        conditionalTokens.Should().ContainSingle(t => t.SegmentKey == "session");
        requiredTokens.Should().HaveCount(2);
        requiredTokens.Select(t => t.SegmentKey).Should().BeEquivalentTo(["model", "mode"]);
    }

    // ── Render ────────────────────────────────────────────────────────────

    [Fact]
    public void Render_OmitsConditionalSegment_WhenValueIsNull()
    {
        var template = FooterTemplate.Parse("{model} │ {session?} │ {mode}");
        var segments = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["model"] = "claude-3",
            ["session"] = null,
            ["mode"] = "Normal",
        };

        var result = template.Render(segments);

        result.Should().Be("claude-3 │ Normal");
    }

    [Fact]
    public void Render_IncludesConditionalSegment_WhenValueIsPresent()
    {
        var template = FooterTemplate.Parse("{model} │ {session?} │ {mode}");
        var segments = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["model"] = "claude-3",
            ["session"] = "abc-123",
            ["mode"] = "Normal",
        };

        var result = template.Render(segments);

        result.Should().Be("claude-3 │ abc-123 │ Normal");
    }

    [Fact]
    public void Render_CollapsesAdjacentSeparators_AroundMultipleOmittedSegments()
    {
        // Both middle segments are conditional and null — double separator collapse
        var template = FooterTemplate.Parse("{a} │ {b?} · {c?} │ {d}");
        var segments = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["a"] = "A",
            ["b"] = null,
            ["c"] = null,
            ["d"] = "D",
        };

        var result = template.Render(segments);

        result.Should().Be("A │ D");
    }

    [Fact]
    public void Render_OmitsUnknownSegmentKey_Silently()
    {
        var template = FooterTemplate.Parse("{known} │ {unknown}");
        var segments = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["known"] = "value",
        };

        var result = template.Render(segments);

        // unknown key → omitted just like a conditional null, separator collapsed
        result.Should().Be("value");
    }

    // ── Malformed input ───────────────────────────────────────────────────

    [Fact]
    public void Parse_MalformedTemplate_TreatsUnclosedBraceAsLiteral()
    {
        var template = FooterTemplate.Parse("{model} │ {unclosed");

        // The well-formed segment is extracted; the unclosed brace is literal
        template.Tokens.Should().ContainSingle(t => !t.IsLiteral && t.SegmentKey == "model");

        var literal = template.Tokens.Last(t => t.IsLiteral);
        literal.LiteralText.Should().Contain("{unclosed");
    }

    // ── Plugin segments ───────────────────────────────────────────────────

    [Fact]
    public void Parse_PluginSegmentKey_ExtractedWithNamespace()
    {
        var template = FooterTemplate.Parse("{plugin:status}");

        var token = template.Tokens.Should().ContainSingle(t => !t.IsLiteral).Which;
        token.SegmentKey.Should().Be("plugin:status");
        token.IsConditional.Should().BeFalse();
    }
}
