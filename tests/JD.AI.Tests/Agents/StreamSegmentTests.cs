using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class StreamSegmentTests
{
    // ── StreamSegmentKind enum ────────────────────────────────────────────

    [Theory]
    [InlineData(StreamSegmentKind.Content, 0)]
    [InlineData(StreamSegmentKind.Thinking, 1)]
    [InlineData(StreamSegmentKind.EnterThinking, 2)]
    [InlineData(StreamSegmentKind.ExitThinking, 3)]
    public void StreamSegmentKind_Values(StreamSegmentKind kind, int expected) =>
        ((int)kind).Should().Be(expected);

    // ── StreamSegment struct ──────────────────────────────────────────────

    [Fact]
    public void StreamSegment_ConstructionWithKindAndText()
    {
        var segment = new StreamSegment(StreamSegmentKind.Content, "Hello world");
        segment.Kind.Should().Be(StreamSegmentKind.Content);
        segment.Text.Should().Be("Hello world");
    }

    [Fact]
    public void StreamSegment_DefaultTextIsEmpty()
    {
        var segment = new StreamSegment(StreamSegmentKind.EnterThinking);
        segment.Kind.Should().Be(StreamSegmentKind.EnterThinking);
        segment.Text.Should().BeEmpty();
    }

    [Fact]
    public void StreamSegment_StructEquality()
    {
        var a = new StreamSegment(StreamSegmentKind.Content, "hi");
        var b = new StreamSegment(StreamSegmentKind.Content, "hi");
        a.Should().Be(b);
    }

    [Fact]
    public void StreamSegment_StructInequality()
    {
        var a = new StreamSegment(StreamSegmentKind.Content, "hi");
        var b = new StreamSegment(StreamSegmentKind.Thinking, "hi");
        a.Should().NotBe(b);
    }
}
