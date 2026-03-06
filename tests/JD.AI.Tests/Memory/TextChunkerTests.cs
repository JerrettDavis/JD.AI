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

    [Fact]
    public void Chunk_ZeroMaxChunkChars_Throws()
    {
        var act = () => TextChunker.Chunk("text", maxChunkChars: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Chunk_NegativeOverlap_Throws()
    {
        var act = () => TextChunker.Chunk("text", maxChunkChars: 100, overlapChars: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Chunk_OverlapEqualToChunkSize_Throws()
    {
        var act = () => TextChunker.Chunk("text", maxChunkChars: 10, overlapChars: 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Chunk_LargeParagraph_SplitsBySentences()
    {
        // One paragraph (no \n\n) that exceeds max, forcing sentence splitting
        var text = "First sentence. Second sentence. Third sentence. Fourth sentence. Fifth sentence.";

        var chunks = TextChunker.Chunk(text, maxChunkChars: 40, overlapChars: 5);

        chunks.Count.Should().BeGreaterThan(1);
        var allText = string.Join(" ", chunks.Select(c => c.Text));
        allText.Should().Contain("First");
        allText.Should().Contain("Fifth");
    }

    [Fact]
    public void Chunk_CharOffsets_AreNonDecreasing()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 10).Select(i => $"Paragraph {i} is here."));

        var chunks = TextChunker.Chunk(text, maxChunkChars: 50, overlapChars: 10);

        chunks.Count.Should().BeGreaterThan(1);
        for (var i = 1; i < chunks.Count; i++)
            chunks[i].CharOffset.Should().BeGreaterThanOrEqualTo(chunks[i - 1].CharOffset);
    }

    [Fact]
    public void Chunk_OverlapZero_NoOverlapContent()
    {
        var text = "Alpha paragraph.\n\nBeta paragraph.\n\nGamma paragraph.";

        var chunks = TextChunker.Chunk(text, maxChunkChars: 25, overlapChars: 0);

        chunks.Count.Should().BeGreaterThan(1);
        // With zero overlap, consecutive chunks should not share content
        for (var i = 1; i < chunks.Count; i++)
            chunks[i].CharOffset.Should().BeGreaterThan(chunks[i - 1].CharOffset);
    }

    [Fact]
    public void Chunk_ExactlyMaxLength_ReturnsSingleChunk()
    {
        var text = new string('a', 100);

        var chunks = TextChunker.Chunk(text, maxChunkChars: 100, overlapChars: 10);

        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be(text);
    }

    [Fact]
    public void Chunk_WindowsLineEndings_SplitsCorrectly()
    {
        var text = "Para one.\r\n\r\nPara two.\r\n\r\nPara three.";

        var chunks = TextChunker.Chunk(text, maxChunkChars: 20, overlapChars: 5);

        chunks.Count.Should().BeGreaterThan(1);
        var allText = string.Join(" ", chunks.Select(c => c.Text));
        allText.Should().Contain("one");
        allText.Should().Contain("three");
    }

    [Fact]
    public void TextChunk_RecordEquality()
    {
        var a = new TextChunk("hello", 0, 0);
        var b = new TextChunk("hello", 0, 0);
        a.Should().Be(b);
    }

    [Fact]
    public void Chunk_DefaultParameters_DoNotThrow()
    {
        var text = new string('x', 5000);
        var act = () => TextChunker.Chunk(text);
        act.Should().NotThrow();
    }
}
