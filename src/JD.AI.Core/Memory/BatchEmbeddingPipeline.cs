using Microsoft.Extensions.Logging;

namespace JD.AI.Core.Memory;

/// <summary>
/// Processes documents through a chunking → embedding → storage pipeline.
/// Handles batching to stay within embedding provider rate limits.
/// </summary>
public sealed class BatchEmbeddingPipeline
{
    private readonly IEmbeddingProvider _embedder;
    private readonly IVectorStore _store;
    private readonly ILogger? _logger;
    private readonly int _batchSize;

    /// <param name="embedder">Embedding provider for vectorization.</param>
    /// <param name="store">Vector store for persistence.</param>
    /// <param name="batchSize">Maximum entries per embedding batch. Default 100.</param>
    /// <param name="logger">Optional logger.</param>
    public BatchEmbeddingPipeline(
        IEmbeddingProvider embedder,
        IVectorStore store,
        int batchSize = 100,
        ILogger? logger = null)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _batchSize = batchSize;
        _logger = logger;
    }

    /// <summary>
    /// Indexes a document by chunking, embedding, and storing.
    /// Returns the number of chunks stored.
    /// </summary>
    public async Task<int> IndexDocumentAsync(
        string documentId,
        string content,
        string? source = null,
        string? category = null,
        int maxChunkChars = TextChunker.DefaultMaxChunkChars,
        int overlapChars = TextChunker.DefaultOverlapChars,
        CancellationToken ct = default)
    {
        var chunks = TextChunker.Chunk(content, maxChunkChars, overlapChars);

        _logger?.LogInformation(
            "Chunked document '{DocId}' into {Count} chunks",
            documentId, chunks.Count);

        var totalStored = 0;

        // Process in batches
        for (var i = 0; i < chunks.Count; i += _batchSize)
        {
            var batch = chunks.Skip(i).Take(_batchSize).ToList();
            var texts = batch.Select(c => c.Text).ToList();

            var embeddings = await _embedder.EmbedAsync(texts, ct).ConfigureAwait(false);

            var entries = batch.Zip(embeddings, (chunk, emb) => new MemoryEntry
            {
                Id = $"{documentId}:chunk:{chunk.Index}",
                Content = chunk.Text,
                Source = source,
                Category = category,
                Embedding = emb,
                Metadata = new Dictionary<string, string>
                {
                    ["document_id"] = documentId,
                    ["chunk_index"] = chunk.Index.ToString(),
                    ["char_offset"] = chunk.CharOffset.ToString(),
                },
            }).ToList();

            await _store.UpsertAsync(entries, ct).ConfigureAwait(false);
            totalStored += entries.Count;

            _logger?.LogDebug(
                "Stored batch {Batch}/{Total} for document '{DocId}'",
                i / _batchSize + 1,
                (chunks.Count + _batchSize - 1) / _batchSize,
                documentId);
        }

        return totalStored;
    }

    /// <summary>
    /// Indexes multiple documents in parallel batches.
    /// </summary>
    public async Task<int> IndexDocumentsAsync(
        IReadOnlyList<(string Id, string Content, string? Source, string? Category)> documents,
        CancellationToken ct = default)
    {
        var total = 0;
        foreach (var (id, content, source, category) in documents)
        {
            total += await IndexDocumentAsync(id, content, source, category, ct: ct)
                .ConfigureAwait(false);
        }

        return total;
    }
}
