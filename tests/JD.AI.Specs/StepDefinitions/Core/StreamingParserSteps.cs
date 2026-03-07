using FluentAssertions;
using JD.AI.Core.Agents;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class StreamingParserSteps
{
    private readonly ScenarioContext _context;

    public StreamingParserSteps(ScenarioContext context) => _context = context;

    [Given(@"a new streaming content parser")]
    public void GivenANewStreamingContentParser()
    {
        var parser = new StreamingContentParser();
        _context.Set(parser);
        _context.Set(new List<StreamSegment>(), "allSegments");
    }

    [When(@"the parser processes chunk ""(.*)""")]
    public void WhenTheParserProcessesChunk(string chunk)
    {
        var parser = _context.Get<StreamingContentParser>();
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var segments = parser.ProcessChunk(chunk);
        allSegments.AddRange(segments);
        _context.Set(segments.ToList(), "lastSegments");
    }

    [When(@"the parser is flushed")]
    public void WhenTheParserIsFlushed()
    {
        var parser = _context.Get<StreamingContentParser>();
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var flushed = parser.Flush();
        allSegments.AddRange(flushed);
        _context.Set(flushed.ToList(), "lastSegments");
    }

    [When(@"the parser is reset")]
    public void WhenTheParserIsReset()
    {
        var parser = _context.Get<StreamingContentParser>();
        parser.Reset();
        _context.Set(new List<StreamSegment>(), "allSegments");
    }

    [Then(@"the segments should contain (\d+) content segment with text ""(.*)""")]
    public void ThenTheSegmentsShouldContainContentSegmentWithText(int count, string text)
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var contentSegments = allSegments.Where(s => s.Kind == StreamSegmentKind.Content).ToList();
        contentSegments.Should().HaveCount(count);
        string.Join("", contentSegments.Select(s => s.Text)).Should().Be(text);
    }

    [Then(@"the segments should contain an EnterThinking segment")]
    public void ThenTheSegmentsShouldContainAnEnterThinkingSegment()
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        allSegments.Should().Contain(s => s.Kind == StreamSegmentKind.EnterThinking);
    }

    [Then(@"the segments should contain a thinking segment with text ""(.*)""")]
    public void ThenTheSegmentsShouldContainThinkingSegmentWithText(string text)
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var thinkingText = string.Join("",
            allSegments.Where(s => s.Kind == StreamSegmentKind.Thinking).Select(s => s.Text));
        thinkingText.Should().Contain(text);
    }

    [Then(@"the segments should contain an ExitThinking segment")]
    public void ThenTheSegmentsShouldContainAnExitThinkingSegment()
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        allSegments.Should().Contain(s => s.Kind == StreamSegmentKind.ExitThinking);
    }

    [Then(@"the segments should contain a content segment with text ""(.*)""")]
    public void ThenTheSegmentsShouldContainContentSegmentText(string text)
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var contentText = string.Join("",
            allSegments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        contentText.Should().Contain(text);
    }

    [Then(@"the parser should have produced content segments containing ""(.*)""")]
    public void ThenTheParserShouldHaveProducedContentSegmentsContaining(string text)
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var contentText = string.Join("",
            allSegments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        contentText.Should().Contain(text);
    }

    [Then(@"the parser should have produced thinking segments containing ""(.*)""")]
    public void ThenTheParserShouldHaveProducedThinkingSegmentsContaining(string text)
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var thinkingText = string.Join("",
            allSegments.Where(s => s.Kind == StreamSegmentKind.Thinking).Select(s => s.Text));
        thinkingText.Should().Contain(text);
    }

    [Then(@"the accumulated content should be ""(.*)""")]
    public void ThenTheAccumulatedContentShouldBe(string expected)
    {
        var allSegments = _context.Get<List<StreamSegment>>("allSegments");
        var contentText = string.Join("",
            allSegments.Where(s => s.Kind == StreamSegmentKind.Content).Select(s => s.Text));
        var thinkingText = string.Join("",
            allSegments.Where(s => s.Kind == StreamSegmentKind.Thinking).Select(s => s.Text));
        (contentText + thinkingText).Should().Be(expected);
    }

    [Then(@"the parser should not be in thinking state")]
    public void ThenTheParserShouldNotBeInThinkingState()
    {
        var parser = _context.Get<StreamingContentParser>();
        parser.IsThinking.Should().BeFalse();
    }
}
