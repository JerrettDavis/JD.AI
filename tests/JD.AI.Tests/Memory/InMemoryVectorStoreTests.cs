using FluentAssertions;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

public sealed class InMemoryVectorStoreTests
{
    private static MemoryEntry CreateEntry(string id, float[] embedding, string? category = null) =>
        new()
        {
            Id = id,
            Content = $"Content for {id}",
            Embedding = embedding,
            Category = category,
        };

    [Fact]
    public async Task UpsertAndSearch_FindsSimilarEntries()
    {
        var store = new InMemoryVectorStore();
        var entries = new[]
        {
            CreateEntry("e1", [1f, 0f, 0f]),
            CreateEntry("e2", [0f, 1f, 0f]),
            CreateEntry("e3", [0.9f, 0.1f, 0f]),
        };
        await store.UpsertAsync(entries);

        var results = await store.SearchAsync([1f, 0f, 0f], topK: 2);

        results.Should().HaveCount(2);
        results[0].Entry.Id.Should().Be("e1"); // Exact match
        results[1].Entry.Id.Should().Be("e3"); // Near match
    }

    [Fact]
    public async Task Search_WithCategoryFilter_FiltersCorrectly()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([
            CreateEntry("e1", [1f, 0f], "code"),
            CreateEntry("e2", [1f, 0f], "docs"),
        ]);

        var results = await store.SearchAsync([1f, 0f], topK: 10, categoryFilter: "docs");

        results.Should().HaveCount(1);
        results[0].Entry.Id.Should().Be("e2");
    }

    [Fact]
    public async Task Delete_RemovesEntries()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([CreateEntry("e1", [1f, 0f])]);

        await store.DeleteAsync(["e1"]);

        (await store.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Count_ReturnsCorrectCount()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([
            CreateEntry("e1", [1f]),
            CreateEntry("e2", [1f]),
        ]);

        (await store.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Upsert_SameId_Replaces()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync([new MemoryEntry { Id = "e1", Content = "v1", Embedding = [1f] }]);
        await store.UpsertAsync([new MemoryEntry { Id = "e1", Content = "v2", Embedding = [1f] }]);

        (await store.CountAsync()).Should().Be(1);
        var results = await store.SearchAsync([1f], topK: 1);
        results[0].Entry.Content.Should().Be("v2");
    }

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var score = InMemoryVectorStore.CosineSimilarity([1f, 2f, 3f], [1f, 2f, 3f]);
        score.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var score = InMemoryVectorStore.CosineSimilarity([1f, 0f], [0f, 1f]);
        score.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        var score = InMemoryVectorStore.CosineSimilarity([1f, 0f], [-1f, 0f]);
        score.Should().BeApproximately(-1.0, 0.001);
    }
}
