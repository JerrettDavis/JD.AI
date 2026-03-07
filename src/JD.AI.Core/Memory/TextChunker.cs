namespace JD.AI.Core.Memory;

/// <summary>
/// Splits text into overlapping chunks suitable for embedding.
/// Supports configurable chunk size and overlap to maintain context
/// across chunk boundaries.
/// </summary>
public static class TextChunker
{
    /// <summary>Default maximum tokens per chunk (approximate, using char-based estimation).</summary>
    public const int DefaultMaxChunkChars = 1500;

    /// <summary>Default overlap between consecutive chunks.</summary>
    public const int DefaultOverlapChars = 200;

    /// <summary>
    /// Splits text into chunks with overlap, respecting paragraph and sentence boundaries.
    /// </summary>
    /// <param name="text">The text to chunk.</param>
    /// <param name="maxChunkChars">Maximum characters per chunk.</param>
    /// <param name="overlapChars">Number of characters to overlap between chunks.</param>
    /// <returns>List of text chunks with metadata.</returns>
    public static IReadOnlyList<TextChunk> Chunk(
        string text,
        int maxChunkChars = DefaultMaxChunkChars,
        int overlapChars = DefaultOverlapChars)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChunkChars);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapChars);
        if (overlapChars >= maxChunkChars)
            throw new ArgumentException("Overlap must be less than chunk size", nameof(overlapChars));

        if (text.Length <= maxChunkChars)
        {
            return [new TextChunk(text.Trim(), 0, 0)];
        }

        var chunks = new List<TextChunk>();
        var paragraphs = SplitParagraphs(text);
        var currentChunk = new System.Text.StringBuilder();
        var chunkIndex = 0;
        var charOffset = 0;

        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length > maxChunkChars && currentChunk.Length > 0)
            {
                // Emit current chunk
                chunks.Add(new TextChunk(currentChunk.ToString().Trim(), chunkIndex, charOffset));
                chunkIndex++;

                // Start new chunk with overlap
                var overlapText = GetOverlap(currentChunk.ToString(), overlapChars);
                charOffset += currentChunk.Length - overlapText.Length;
                currentChunk.Clear();
                currentChunk.Append(overlapText);
            }

            // If a single paragraph exceeds max, split by sentences
            if (paragraph.Length > maxChunkChars)
            {
                var sentences = SplitSentences(paragraph);
                foreach (var sentence in sentences)
                {
                    if (currentChunk.Length + sentence.Length > maxChunkChars && currentChunk.Length > 0)
                    {
                        chunks.Add(new TextChunk(currentChunk.ToString().Trim(), chunkIndex, charOffset));
                        chunkIndex++;
                        var overlap = GetOverlap(currentChunk.ToString(), overlapChars);
                        charOffset += currentChunk.Length - overlap.Length;
                        currentChunk.Clear();
                        currentChunk.Append(overlap);
                    }

                    currentChunk.Append(sentence);
                }
            }
            else
            {
                currentChunk.Append(paragraph);
                currentChunk.AppendLine();
            }
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk(currentChunk.ToString().Trim(), chunkIndex, charOffset));
        }

        return chunks.AsReadOnly();
    }

    private static string[] SplitParagraphs(string text)
    {
        return text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
    }

    private static List<string> SplitSentences(string text)
    {
        var sentences = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '.' or '!' or '?' && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
            {
                sentences.Add(text[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < text.Length)
            sentences.Add(text[start..]);

        return sentences;
    }

    private static string GetOverlap(string text, int overlapChars)
    {
        if (text.Length <= overlapChars)
            return text;

        return text[^overlapChars..];
    }
}

/// <summary>
/// A chunk of text with position metadata.
/// </summary>
/// <param name="Text">The chunk content.</param>
/// <param name="Index">Zero-based chunk index within the source document.</param>
/// <param name="CharOffset">Character offset from the start of the source document.</param>
public sealed record TextChunk(string Text, int Index, int CharOffset);
