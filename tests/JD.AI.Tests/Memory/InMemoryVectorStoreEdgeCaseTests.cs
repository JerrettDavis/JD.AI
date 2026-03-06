using FluentAssertions;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

public sealed class InMemoryVectorStoreEdgeCaseTests
{
    // ── CosineSimilarity edge cases ───────────────────────────────────────

    [Fact]
    public void CosineSimilarity_DifferentLengthVectors_Throws()
    {
        var act = () => InMemoryVectorStore.CosineSimilarity([1f, 2f], [1f, 2f, 3f]);
        act.Should().Throw<ArgumentException>().WithParameterName("b");
    }

    [Fact]
    public void CosineSimilarity_ZeroVectors_ReturnsZero()
    {
        var score = InMemoryVectorStore.CosineSimilarity([0f, 0f, 0f], [0f, 0f, 0f]);
        score.Should().Be(0);
    }

    [Fact]
    public void CosineSimilarity_OneZeroVector_ReturnsZero()
    {
        var score = InMemoryVectorStore.CosineSimilarity([1f, 2f, 3f], [0f, 0f, 0f]);
        score.Should().Be(0);
    }

    [Fact]
    public void CosineSimilarity_LargeVectors_UsesSimdPath()
    {
        // Create vectors large enough to hit the SIMD path (>= Vector<float>.Count)
        var size = 64;
        var a = new float[size];
        var b = new float[size];
        for (var i = 0; i < size; i++)
        {
            a[i] = 1f;
            b[i] = 1f;
        }

        var score = InMemoryVectorStore.CosineSimilarity(a, b);
        score.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_LargeOrthogonalVectors()
    {
        var size = 64;
        var a = new float[size];
        var b = new float[size];
        a[0] = 1f;
        b[size - 1] = 1f;

        var score = InMemoryVectorStore.CosineSimilarity(a, b);
        score.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ReturnsZero()
    {
        var score = InMemoryVectorStore.CosineSimilarity([], []);
        score.Should().Be(0);
    }

    [Fact]
    public void CosineSimilarity_SingleElement_PositiveValues()
    {
        var score = InMemoryVectorStore.CosineSimilarity([5f], [3f]);
        score.Should().BeApproximately(1.0, 0.001);
    }

    // ── Search edge cases ─────────────────────────────────────────────────

    [Fact]
    public async Task Search_NoEmbedding_ExcludedFromResults()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([
            new MemoryEntry { Id = "e1", Content = "no embedding" },
            new MemoryEntry { Id = "e2", Content = "has embedding", Embedding = [1f, 0f] },
        ]);

        var results = await store.SearchAsync([1f, 0f], topK: 10);
        results.Should().HaveCount(1);
        results[0].Entry.Id.Should().Be("e2");
    }

    [Fact]
    public async Task Search_EmptyStore_ReturnsEmpty()
    {
        var store = new InMemoryVectorStore();
        var results = await store.SearchAsync([1f, 0f], topK: 5);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Count_EmptyStore_ReturnsZero()
    {
        var store = new InMemoryVectorStore();
        store.Count.Should().Be(0);
        (await store.CountAsync()).Should().Be(0);
    }
}
