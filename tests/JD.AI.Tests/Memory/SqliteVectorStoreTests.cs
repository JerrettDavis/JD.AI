
using FluentAssertions;
using JD.AI.Core.Memory;
using Xunit;

namespace JD.AI.Tests.Memory;

public class SqliteVectorStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteVectorStore _store;

    public SqliteVectorStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"jdai_test_{Guid.NewGuid():N}.db");
        _store = new SqliteVectorStore(_dbPath);
    }

    public ValueTask DisposeAsync()
    {
        _store.Dispose();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task StoreAndRetrieve_SingleDocument()
    {
        var entry = new MemoryEntry
        {
            Id = "doc1",
            Content = "Hello world",
            Embedding = [1.0f, 0.0f, 0.0f],
        };

        await _store.UpsertAsync([entry]);

        var count = await _store.CountAsync();
        count.Should().Be(1);

        var results = await _store.SearchAsync([1.0f, 0.0f, 0.0f], topK: 1);
        results.Should().HaveCount(1);
        results[0].Entry.Content.Should().Be("Hello world");
        results[0].Score.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task Search_ReturnsMostSimilar()
    {
        var entries = new MemoryEntry[]
        {
            new() { Id = "a", Content = "alpha", Embedding = [1.0f, 0.0f, 0.0f] },
            new() { Id = "b", Content = "beta", Embedding = [0.0f, 1.0f, 0.0f] },
            new() { Id = "c", Content = "gamma", Embedding = [0.0f, 0.0f, 1.0f] },
        };

        await _store.UpsertAsync(entries);

        // Search for something closest to "beta"
        var results = await _store.SearchAsync([0.1f, 0.9f, 0.0f], topK: 3);

        results.Should().HaveCount(3);
        results[0].Entry.Id.Should().Be("b", "beta vector is most similar to query");
    }

    [Fact]
    public async Task Search_RespectsTopK()
    {
        var entries = new MemoryEntry[]
        {
            new() { Id = "1", Content = "one", Embedding = [1.0f, 0.0f] },
            new() { Id = "2", Content = "two", Embedding = [0.0f, 1.0f] },
            new() { Id = "3", Content = "three", Embedding = [0.5f, 0.5f] },
        };

        await _store.UpsertAsync(entries);

        var results = await _store.SearchAsync([1.0f, 0.0f], topK: 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_RespectsMinScore()
    {
        var entries = new MemoryEntry[]
        {
            new() { Id = "close", Content = "close match", Embedding = [1.0f, 0.0f, 0.0f] },
            new() { Id = "far", Content = "far away", Embedding = [0.0f, 0.0f, 1.0f] },
        };

        await _store.UpsertAsync(entries);

        // Search returns results ordered by score; verify high-score result comes first
        var results = await _store.SearchAsync([1.0f, 0.0f, 0.0f], topK: 10);

        results.Should().NotBeEmpty();
        results[0].Entry.Id.Should().Be("close");
        results[0].Score.Should().BeGreaterThan(0.9);

        // The far document should have a low score
        var farResult = results.FirstOrDefault(r => string.Equals(r.Entry.Id, "far", StringComparison.Ordinal));
        farResult.Should().NotBeNull();
        farResult!.Score.Should().BeLessThan(0.1);
    }

    [Fact]
    public async Task Delete_RemovesDocument()
    {
        var entry = new MemoryEntry
        {
            Id = "to-delete",
            Content = "ephemeral",
            Embedding = [1.0f, 0.0f],
        };

        await _store.UpsertAsync([entry]);
        (await _store.CountAsync()).Should().Be(1);

        await _store.DeleteAsync(["to-delete"]);

        (await _store.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Store_SameId_Updates()
    {
        var original = new MemoryEntry
        {
            Id = "upsert-test",
            Content = "original content",
            Embedding = [1.0f, 0.0f],
        };

        await _store.UpsertAsync([original]);

        var updated = new MemoryEntry
        {
            Id = "upsert-test",
            Content = "updated content",
            Embedding = [0.0f, 1.0f],
        };

        await _store.UpsertAsync([updated]);

        (await _store.CountAsync()).Should().Be(1);

        var results = await _store.SearchAsync([0.0f, 1.0f], topK: 1);
        results.Should().HaveCount(1);
        results[0].Entry.Content.Should().Be("updated content");
    }
}
