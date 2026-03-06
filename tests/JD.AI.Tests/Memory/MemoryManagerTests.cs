using FluentAssertions;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

public sealed class MemoryManagerTests
{
    private sealed class FakeEmbedder : IEmbeddingProvider
    {
        public string ProviderName => "fake";
        public int Dimensions => 3;

        public Task<IReadOnlyList<float[]>> EmbedAsync(
            IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            var results = texts.Select(_ => new float[] { 1f, 0f, 0f }).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(results);
        }
    }

    private static MemoryManager CreateManager() =>
        new(new FakeEmbedder(), new InMemoryVectorStore());

    // ── IndexAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IndexAsync_StoresEntries()
    {
        var store = new InMemoryVectorStore();
        var manager = new MemoryManager(new FakeEmbedder(), store);

        await manager.IndexAsync([("doc-1", "Hello world", null, null)]);

        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task IndexAsync_MultipleItems()
    {
        var store = new InMemoryVectorStore();
        var manager = new MemoryManager(new FakeEmbedder(), store);

        await manager.IndexAsync([
            ("doc-1", "Hello", "src", "code"),
            ("doc-2", "World", "src", "code"),
        ]);

        store.Count.Should().Be(2);
    }

    [Fact]
    public async Task IndexAsync_EmptyList_StoresNothing()
    {
        var store = new InMemoryVectorStore();
        var manager = new MemoryManager(new FakeEmbedder(), store);

        await manager.IndexAsync([]);

        store.Count.Should().Be(0);
    }

    // ── SearchAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ReturnsResults()
    {
        var manager = CreateManager();

        await manager.IndexAsync([("doc-1", "Hello world", null, null)]);
        var results = await manager.SearchAsync("Hello");

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyStore_ReturnsEmpty()
    {
        var manager = CreateManager();

        var results = await manager.SearchAsync("anything");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK()
    {
        var manager = CreateManager();

        await manager.IndexAsync([
            ("doc-1", "First", null, null),
            ("doc-2", "Second", null, null),
            ("doc-3", "Third", null, null),
        ]);

        var results = await manager.SearchAsync("test", topK: 2);

        results.Should().HaveCountLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task SearchAsync_CategoryFilter()
    {
        var manager = CreateManager();

        await manager.IndexAsync([
            ("doc-1", "First", null, "code"),
            ("doc-2", "Second", null, "docs"),
        ]);

        var results = await manager.SearchAsync("test", categoryFilter: "code");

        results.Should().AllSatisfy(r => r.Entry.Category.Should().Be("code"));
    }
}
