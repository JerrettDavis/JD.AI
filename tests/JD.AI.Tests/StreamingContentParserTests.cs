using JD.AI.Tui.Rendering;

namespace JD.AI.Tui.Tests;

public sealed class StreamingContentParserTests
{
    [Fact]
    public void PlainText_EmitsContentOnly()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("Hello world");

        Assert.Single(segments);
        Assert.Equal(StreamSegmentKind.Content, segments[0].Kind);
        Assert.Equal("Hello world", segments[0].Text);
    }

    [Fact]
    public void ThinkTag_InSingleChunk_EmitsEnterThinkingAndThinking()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<think>reasoning here");

        Assert.Equal(2, segments.Count);
        Assert.Equal(StreamSegmentKind.EnterThinking, segments[0].Kind);
        Assert.Equal(StreamSegmentKind.Thinking, segments[1].Kind);
        Assert.Equal("reasoning here", segments[1].Text);
    }

    [Fact]
    public void FullThinkBlock_InSingleChunk_EmitsAllTransitions()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<think>reasoning</think>answer");

        Assert.Equal(4, segments.Count);
        Assert.Equal(StreamSegmentKind.EnterThinking, segments[0].Kind);
        Assert.Equal(StreamSegmentKind.Thinking, segments[1].Kind);
        Assert.Equal("reasoning", segments[1].Text);
        Assert.Equal(StreamSegmentKind.ExitThinking, segments[2].Kind);
        Assert.Equal(StreamSegmentKind.Content, segments[3].Kind);
        Assert.Equal("answer", segments[3].Text);
    }

    [Fact]
    public void ThinkTag_SpansMultipleChunks()
    {
        var parser = new StreamingContentParser();

        // "<think>" split across 3 chunks
        var s1 = parser.ProcessChunk("<thi");
        Assert.Empty(s1); // buffered

        var s2 = parser.ProcessChunk("nk>");
        Assert.Single(s2);
        Assert.Equal(StreamSegmentKind.EnterThinking, s2[0].Kind);

        var s3 = parser.ProcessChunk("deep thought");
        Assert.Single(s3);
        Assert.Equal(StreamSegmentKind.Thinking, s3[0].Kind);
        Assert.Equal("deep thought", s3[0].Text);
    }

    [Fact]
    public void CloseTag_SpansMultipleChunks()
    {
        var parser = new StreamingContentParser();

        // Enter thinking first
        parser.ProcessChunk("<think>thinking");

        // "</think>" split across chunks
        var s1 = parser.ProcessChunk("</thi");
        Assert.Empty(s1);

        var s2 = parser.ProcessChunk("nk>content");
        Assert.Equal(2, s2.Count);
        Assert.Equal(StreamSegmentKind.ExitThinking, s2[0].Kind);
        Assert.Equal(StreamSegmentKind.Content, s2[1].Kind);
        Assert.Equal("content", s2[1].Text);
    }

    [Fact]
    public void FalsePositiveTag_FlushesAsContent()
    {
        var parser = new StreamingContentParser();

        // "<thin" looks like start of <think> but "x" breaks it
        var segments = parser.ProcessChunk("<thinx");

        Assert.Single(segments);
        Assert.Equal(StreamSegmentKind.Content, segments[0].Kind);
        Assert.Equal("<thinx", segments[0].Text);
    }

    [Fact]
    public void HtmlTag_NotThinkTag_FlushesAsContent()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<div>hello</div>");

        // "<d" doesn't match "<think>", so flush happens early
        Assert.True(segments.Count >= 1);
        var combined = string.Concat(segments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        Assert.Equal("<div>hello</div>", combined);
    }

    [Fact]
    public void MultipleThinkBlocks()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<think>thought1</think>action<think>thought2</think>result");

        var thinking = segments.Where(s => s.Kind == StreamSegmentKind.Thinking).Select(s => s.Text).ToList();
        var content = segments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text).ToList();

        Assert.Equal(["thought1", "thought2"], thinking);
        Assert.Equal(["action", "result"], content);
    }

    [Fact]
    public void IsThinking_TracksState()
    {
        var parser = new StreamingContentParser();

        Assert.False(parser.IsThinking);

        parser.ProcessChunk("<think>");
        Assert.True(parser.IsThinking);

        parser.ProcessChunk("stuff</think>");
        Assert.False(parser.IsThinking);
    }

    [Fact]
    public void Flush_EmitsBufferedContent()
    {
        var parser = new StreamingContentParser();

        // Leave an incomplete tag at the end
        parser.ProcessChunk("hello <thi");

        var flush = parser.Flush();
        Assert.Single(flush);
        Assert.Equal(StreamSegmentKind.Content, flush[0].Kind);
        Assert.Equal("<thi", flush[0].Text);
    }

    [Fact]
    public void Flush_EmptyWhenNothingBuffered()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("hello world");

        var flush = parser.Flush();
        Assert.Empty(flush);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var parser = new StreamingContentParser();
        parser.ProcessChunk("<think>partial");

        Assert.True(parser.IsThinking);

        parser.Reset();

        Assert.False(parser.IsThinking);

        var segments = parser.ProcessChunk("fresh content");
        Assert.Single(segments);
        Assert.Equal(StreamSegmentKind.Content, segments[0].Kind);
    }

    [Fact]
    public void CharByChar_Streaming_Works()
    {
        var parser = new StreamingContentParser();
        var allSegments = new List<StreamSegment>();

        // Simulate char-by-char streaming of "<think>hi</think>ok"
        foreach (var c in "<think>hi</think>ok")
        {
            foreach (var seg in parser.ProcessChunk(c.ToString()))
                allSegments.Add(seg);
        }

        foreach (var seg in parser.Flush())
            allSegments.Add(seg);

        var thinking = string.Concat(allSegments.Where(s => s.Kind == StreamSegmentKind.Thinking).Select(s => s.Text));
        var content = string.Concat(allSegments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        var enterCount = allSegments.Count(s => s.Kind == StreamSegmentKind.EnterThinking);
        var exitCount = allSegments.Count(s => s.Kind == StreamSegmentKind.ExitThinking);

        Assert.Equal("hi", thinking);
        Assert.Equal("ok", content);
        Assert.Equal(1, enterCount);
        Assert.Equal(1, exitCount);
    }

    [Fact]
    public void ThinkingOnly_NoContent()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("<think>all thinking</think>");

        var contentSegments = segments.Where(s => s.Kind == StreamSegmentKind.Content).ToList();
        Assert.Empty(contentSegments);

        var thinking = segments.Where(s => s.Kind == StreamSegmentKind.Thinking).Select(s => s.Text).ToList();
        Assert.Equal(["all thinking"], thinking);
    }

    [Fact]
    public void ContentOnly_NoThinking()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("just plain content");

        Assert.Single(segments);
        Assert.Equal(StreamSegmentKind.Content, segments[0].Kind);
        Assert.DoesNotContain(segments, s => s.Kind == StreamSegmentKind.EnterThinking);
    }

    [Fact]
    public void EmptyChunk_ReturnsEmpty()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("");

        Assert.Empty(segments);
    }

    [Fact]
    public void LessThanSign_NotFollowedByTag_FlushesCorrectly()
    {
        var parser = new StreamingContentParser();
        var segments = parser.ProcessChunk("x < y");

        var combined = string.Concat(segments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        Assert.Equal("x < y", combined);
    }

    [Fact]
    public void NestedAngleBrackets_NotConfused()
    {
        var parser = new StreamingContentParser();
        // Regular code with angle brackets shouldn't trigger thinking
        var segments = parser.ProcessChunk("List<int> items = new();");

        var combined = string.Concat(segments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        Assert.Equal("List<int> items = new();", combined);
        Assert.DoesNotContain(segments, s => s.Kind == StreamSegmentKind.EnterThinking);
    }

    [Fact]
    public void StreamSegment_RecordEquality()
    {
        var a = new StreamSegment(StreamSegmentKind.Content, "hello");
        var b = new StreamSegment(StreamSegmentKind.Content, "hello");
        Assert.Equal(a, b);
    }
}
