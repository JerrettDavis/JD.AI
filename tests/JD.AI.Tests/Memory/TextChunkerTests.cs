using FluentAssertions;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var chunks = TextChunker.Chunk("Hello world", maxChunkChars: 100, overlapChars: 20);

        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be("Hello world");
        chunks[0].Index.Should().Be(0);
    }

    [Fact]
    public void Chunk_LongText_SplitsIntoMultipleChunks()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 20).Select(i => $"Paragraph {i} with some content."));

        var chunks = TextChunker.Chunk(text, maxChunkChars: 100, overlapChars: 20);

        chunks.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Chunk_PreservesAllContent()
    {
        var text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";

        var chunks = TextChunker.Chunk(text, maxChunkChars: 30, overlapChars: 5);

        // All paragraphs should appear in at least one chunk
        var allText = string.Join(" ", chunks.Select(c => c.Text));
        allText.Should().Contain("First");
        allText.Should().Contain("Second");
        allText.Should().Contain("Third");
    }

    [Fact]
    public void Chunk_ChunkIndicesAreSequential()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 10).Select(i => new string('x', 50)));

        var chunks = TextChunker.Chunk(text, maxChunkChars: 100, overlapChars: 10);

        for (var i = 0; i < chunks.Count; i++)
            chunks[i].Index.Should().Be(i);
    }

    [Fact]
    public void Chunk_NullText_Throws()
    {
        var act = () => TextChunker.Chunk(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Chunk_OverlapGreaterThanChunkSize_Throws()
    {
        var act = () => TextChunker.Chunk("text", maxChunkChars: 10, overlapChars: 15);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Chunk_EmptyText_ReturnsSingleEmptyChunk()
    {
        var chunks = TextChunker.Chunk("");
        chunks.Should().HaveCount(1);
    }
}
