using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

/// <summary>
/// Tests the JD.AI.Core.Agents.StreamingContentParser (the Core copy).
/// The Rendering copy is tested in StreamingContentParserTests.cs.
/// </summary>
public sealed class CoreStreamingContentParserTests
{
    // ── Plain content ────────────────────────────────────────────────────

    [Fact]
    public void PlainText_EmitsContentOnly()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("Hello world");

        segments.Should().ContainSingle()
            .Which.Should().Be(new StreamSegment(StreamSegmentKind.Content, "Hello world"));
    }

    [Fact]
    public void EmptyChunk_EmitsNothing()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("");

        segments.Should().BeEmpty();
    }

    // ── Think tag in single chunk ────────────────────────────────────────

    [Fact]
    public void ThinkTag_InSingleChunk_EmitsTransitions()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<think>reasoning</think>");

        segments.Should().HaveCount(3);
        segments[0].Kind.Should().Be(StreamSegmentKind.EnterThinking);
        segments[1].Should().Be(new StreamSegment(StreamSegmentKind.Thinking, "reasoning"));
        segments[2].Kind.Should().Be(StreamSegmentKind.ExitThinking);
    }

    [Fact]
    public void ThinkTag_WithSurroundingContent()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("before<think>inside</think>after");

        segments.Should().HaveCount(5);
        segments[0].Should().Be(new StreamSegment(StreamSegmentKind.Content, "before"));
        segments[1].Kind.Should().Be(StreamSegmentKind.EnterThinking);
        segments[2].Should().Be(new StreamSegment(StreamSegmentKind.Thinking, "inside"));
        segments[3].Kind.Should().Be(StreamSegmentKind.ExitThinking);
        segments[4].Should().Be(new StreamSegment(StreamSegmentKind.Content, "after"));
    }

    // ── Tag split across chunks ──────────────────────────────────────────

    [Fact]
    public void OpenTag_SplitAcrossChunks()
    {
        var parser = new StreamingContentParser();

        var seg1 = parser.ProcessChunk("<thi");
        seg1.Should().BeEmpty(); // still buffering

        var seg2 = parser.ProcessChunk("nk>");
        seg2.Should().ContainSingle()
            .Which.Kind.Should().Be(StreamSegmentKind.EnterThinking);

        parser.IsThinking.Should().BeTrue();
    }

    [Fact]
    public void CloseTag_SplitAcrossChunks()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("<think>thinking");

        var seg1 = parser.ProcessChunk("</thi");
        // Thinking content should be emitted, close tag being buffered
        seg1.Should().BeEmpty();

        var seg2 = parser.ProcessChunk("nk>");
        seg2.Should().ContainSingle()
            .Which.Kind.Should().Be(StreamSegmentKind.ExitThinking);

        parser.IsThinking.Should().BeFalse();
    }

    // ── Non-matching angle bracket ───────────────────────────────────────

    [Fact]
    public void AngleBracket_NotATag_FlushedAsContent()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<div>hello");

        // <div> doesn't match <think>, so it's flushed as content
        segments.Should().HaveCount(1);
        segments[0].Kind.Should().Be(StreamSegmentKind.Content);
        segments[0].Text.Should().Contain("<div>");
    }

    [Fact]
    public void LessThan_FollowedByNonTag()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("a < b");

        segments.Should().ContainSingle()
            .Which.Kind.Should().Be(StreamSegmentKind.Content);
    }

    // ── IsThinking state ─────────────────────────────────────────────────

    [Fact]
    public void IsThinking_InitiallyFalse()
    {
        var parser = new StreamingContentParser();
        parser.IsThinking.Should().BeFalse();
    }

    [Fact]
    public void IsThinking_TrueAfterOpenTag()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("<think>");
        parser.IsThinking.Should().BeTrue();
    }

    [Fact]
    public void IsThinking_FalseAfterCloseTag()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("<think>stuff</think>");
        parser.IsThinking.Should().BeFalse();
    }

    // ── Flush ────────────────────────────────────────────────────────────

    [Fact]
    public void Flush_EmitsBufferedContent()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("<thi"); // incomplete tag

        var flushed = parser.Flush();
        flushed.Should().ContainSingle()
            .Which.Should().Be(new StreamSegment(StreamSegmentKind.Content, "<thi"));
    }

    [Fact]
    public void Flush_EmptyWhenNothingBuffered()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("hello");

        var flushed = parser.Flush();
        flushed.Should().BeEmpty();
    }

    // ── Reset ────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsState()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("<think>inside");
        parser.IsThinking.Should().BeTrue();

        parser.Reset();
        parser.IsThinking.Should().BeFalse();

        // After reset, content is treated as regular
        var segments = parser.ProcessChunk("after reset");
        segments.Should().ContainSingle()
            .Which.Kind.Should().Be(StreamSegmentKind.Content);
    }

    // ── Segment merging ──────────────────────────────────────────────────

    [Fact]
    public void ConsecutiveContent_MergedIntoSingleSegment()
    {
        var parser = new StreamingContentParser();
        // Each char is processed individually, Content segments should merge
        var segments = parser.ProcessChunk("abc");

        segments.Should().ContainSingle()
            .Which.Text.Should().Be("abc");
    }

    [Fact]
    public void MultipleThinkBlocks_InOneChunk()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<think>a</think>mid<think>b</think>");

        segments.Should().HaveCount(7);
        segments[0].Kind.Should().Be(StreamSegmentKind.EnterThinking);
        segments[1].Should().Be(new StreamSegment(StreamSegmentKind.Thinking, "a"));
        segments[2].Kind.Should().Be(StreamSegmentKind.ExitThinking);
        segments[3].Should().Be(new StreamSegment(StreamSegmentKind.Content, "mid"));
        segments[4].Kind.Should().Be(StreamSegmentKind.EnterThinking);
        segments[5].Should().Be(new StreamSegment(StreamSegmentKind.Thinking, "b"));
        segments[6].Kind.Should().Be(StreamSegmentKind.ExitThinking);
    }

    // ── Reuse of returned list ───────────────────────────────────────────

    [Fact]
    public void ReturnedList_IsReusedAcrossCalls()
    {
        var parser = new StreamingContentParser();
        var seg1 = parser.ProcessChunk("first");
        seg1.Should().ContainSingle();

        var seg2 = parser.ProcessChunk("second");
        // seg1 and seg2 share the same underlying list, so seg1 is now stale
        seg2.Should().ContainSingle();
        seg2[0].Text.Should().Be("second");
    }
}
