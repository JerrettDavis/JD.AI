using FluentAssertions;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

public sealed class BatchEmbeddingPipelineTests
{
    private static readonly float[] FakeEmbedding = [1f, 0f, 0f];

    private sealed class FakeEmbedder : IEmbeddingProvider
    {
        public string ProviderName => "fake";
        public int Dimensions => 3;
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<float[]>> EmbedAsync(
            IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            CallCount++;
            var results = texts.Select(_ => FakeEmbedding).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(results);
        }
    }

    private sealed class FakeVectorStore : IVectorStore
    {
        public List<MemoryEntry> Stored { get; } = [];

        public Task UpsertAsync(IReadOnlyList<MemoryEntry> entries, CancellationToken ct = default)
        {
            Stored.AddRange(entries);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
            float[] queryEmbedding, int topK = 5, string? categoryFilter = null,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MemorySearchResult>>([]);

        public Task DeleteAsync(IReadOnlyList<string> ids, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<long> CountAsync(CancellationToken ct = default) =>
            Task.FromResult((long)Stored.Count);
    }

    // ── Constructor ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullEmbedder_Throws()
    {
        var act = () => new BatchEmbeddingPipeline(null!, new FakeVectorStore());
        act.Should().Throw<ArgumentNullException>().WithParameterName("embedder");
    }

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        var act = () => new BatchEmbeddingPipeline(new FakeEmbedder(), null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("store");
    }

    // ── IndexDocumentAsync ────────────────────────────────────────────────

    [Fact]
    public async Task IndexDocument_ShortText_ReturnsSingleChunk()
    {
        var embedder = new FakeEmbedder();
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(embedder, store);

        var count = await pipeline.IndexDocumentAsync("doc-1", "Hello world");

        count.Should().Be(1);
        store.Stored.Should().HaveCount(1);
    }

    [Fact]
    public async Task IndexDocument_SetsDocumentIdInMetadata()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        await pipeline.IndexDocumentAsync("doc-42", "Some text");

        store.Stored[0].Metadata.Should().ContainKey("document_id");
        store.Stored[0].Metadata["document_id"].Should().Be("doc-42");
    }

    [Fact]
    public async Task IndexDocument_SetsChunkIndexInMetadata()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        await pipeline.IndexDocumentAsync("doc-1", "Short text");

        store.Stored[0].Metadata.Should().ContainKey("chunk_index");
        store.Stored[0].Metadata["chunk_index"].Should().Be("0");
    }

    [Fact]
    public async Task IndexDocument_SetsSourceAndCategory()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        await pipeline.IndexDocumentAsync("doc-1", "Hello", source: "readme", category: "docs");

        store.Stored[0].Source.Should().Be("readme");
        store.Stored[0].Category.Should().Be("docs");
    }

    [Fact]
    public async Task IndexDocument_SetsEmbedding()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        await pipeline.IndexDocumentAsync("doc-1", "Hello");

        store.Stored[0].Embedding.Should().NotBeNull();
        store.Stored[0].Embedding.Should().HaveCount(3);
    }

    [Fact]
    public async Task IndexDocument_LongText_CreatesMultipleChunks()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        // Create text large enough to require chunking (default 1500 chars per chunk)
        var longText = string.Join("\n\n", Enumerable.Range(0, 50).Select(i => new string('x', 100)));
        var count = await pipeline.IndexDocumentAsync("doc-1", longText);

        count.Should().BeGreaterThan(1);
        store.Stored.Should().HaveCount(count);
    }

    [Fact]
    public async Task IndexDocument_BatchSize_ControlsEmbedderCalls()
    {
        var embedder = new FakeEmbedder();
        var store = new FakeVectorStore();
        // Use tiny batch size to force multiple embed calls
        var pipeline = new BatchEmbeddingPipeline(embedder, store, batchSize: 1);

        // Create text that will produce at least 2 chunks
        var longText = string.Join("\n\n", Enumerable.Range(0, 50).Select(i => new string('x', 100)));
        await pipeline.IndexDocumentAsync("doc-1", longText);

        embedder.CallCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task IndexDocument_ChunkIdsContainDocumentId()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        await pipeline.IndexDocumentAsync("my-doc", "Hello");

        store.Stored.Should().AllSatisfy(e => e.Id.Should().StartWith("my-doc:"));
    }

    // ── IndexDocumentsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task IndexDocuments_MultipleDocuments_ReturnsTotal()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        var docs = new List<(string Id, string Content, string? Source, string? Category)>
        {
            ("doc-1", "First", null, null),
            ("doc-2", "Second", null, null),
            ("doc-3", "Third", null, null),
        };

        var total = await pipeline.IndexDocumentsAsync(docs);

        total.Should().Be(3);
        store.Stored.Should().HaveCount(3);
    }

    [Fact]
    public async Task IndexDocuments_Empty_ReturnsZero()
    {
        var store = new FakeVectorStore();
        var pipeline = new BatchEmbeddingPipeline(new FakeEmbedder(), store);

        var total = await pipeline.IndexDocumentsAsync([]);

        total.Should().Be(0);
        store.Stored.Should().BeEmpty();
    }
}
