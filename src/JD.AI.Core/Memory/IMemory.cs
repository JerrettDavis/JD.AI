namespace JD.AI.Core.Memory;

/// <summary>
/// Abstraction for text embedding providers (OpenAI, Ollama, Voyage, etc.).
/// </summary>
public interface IEmbeddingProvider
{
    string ProviderName { get; }

    /// <summary>Generates embeddings for a batch of texts.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);

    /// <summary>Generates an embedding for a single text.</summary>
    async Task<float[]> EmbedSingleAsync(string text, CancellationToken ct = default)
    {
        var results = await EmbedAsync([text], ct);
        return results[0];
    }

    /// <summary>The dimensionality of the embedding vectors.</summary>
    int Dimensions { get; }
}

/// <summary>
/// A chunk of content stored in the vector store with its embedding.
/// </summary>
public record MemoryEntry
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public string? Source { get; init; }
    public string? Category { get; init; }
    public float[]? Embedding { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Search result from vector similarity search.
/// </summary>
public record MemorySearchResult(MemoryEntry Entry, double Score);

/// <summary>
/// Vector store abstraction for semantic memory.
/// </summary>
public interface IVectorStore
{
    /// <summary>Upserts entries with their embeddings.</summary>
    Task UpsertAsync(IReadOnlyList<MemoryEntry> entries, CancellationToken ct = default);

    /// <summary>Searches for similar entries by embedding vector.</summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        string? categoryFilter = null,
        CancellationToken ct = default);

    /// <summary>Deletes entries by IDs.</summary>
    Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct = default);

    /// <summary>Returns the total number of stored entries.</summary>
    Task<long> CountAsync(CancellationToken ct = default);
}

/// <summary>
/// Unified memory manager that combines embedding + vector store.
/// </summary>
public sealed class MemoryManager
{
    private readonly IEmbeddingProvider _embedder;
    private readonly IVectorStore _store;

    public MemoryManager(IEmbeddingProvider embedder, IVectorStore store)
    {
        _embedder = embedder;
        _store = store;
    }

    /// <summary>Indexes content into the vector store.</summary>
    public async Task IndexAsync(
        IReadOnlyList<(string Id, string Content, string? Source, string? Category)> items,
        CancellationToken ct = default)
    {
        var texts = items.Select(i => i.Content).ToList();
        var embeddings = await _embedder.EmbedAsync(texts, ct);

        var entries = items.Zip(embeddings, (item, emb) => new MemoryEntry
        {
            Id = item.Id,
            Content = item.Content,
            Source = item.Source,
            Category = item.Category,
            Embedding = emb
        }).ToList();

        await _store.UpsertAsync(entries, ct);
    }

    /// <summary>Searches memory with a natural language query.</summary>
    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        int topK = 5,
        string? categoryFilter = null,
        CancellationToken ct = default)
    {
        var queryEmb = await _embedder.EmbedSingleAsync(query, ct);
        return await _store.SearchAsync(queryEmb, topK, categoryFilter, ct);
    }
}
