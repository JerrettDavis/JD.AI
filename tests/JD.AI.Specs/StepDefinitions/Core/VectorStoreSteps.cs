using FluentAssertions;
using JD.AI.Core.Memory;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class VectorStoreSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private SqliteVectorStore? _store;

    public VectorStoreSteps(ScenarioContext context) => _context = context;

    [Given(@"a SQLite vector store with a temporary database")]
    public void GivenASqliteVectorStoreWithTempDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jdai-vec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "vectors.db");
        _store = new SqliteVectorStore(dbPath);
        _context.Set(_store, "VectorStore");
        _context.Set(dir, "VecDir");
    }

    [Given(@"I have stored entry ""([^""]+)"" with embedding \[(.+)\]")]
    public async Task GivenIHaveStoredEntryWithEmbedding(string id, string embeddingStr)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var embedding = embeddingStr.Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var entry = new MemoryEntry
        {
            Id = id,
            Content = $"content-{id}",
            Embedding = embedding,
        };
        await store.UpsertAsync([entry]);
    }

    [Given(@"I have stored entry ""([^""]+)"" in category ""([^""]+)"" with embedding \[(.+)\]")]
    public async Task GivenIHaveStoredEntryInCategoryWithEmbedding(string id, string category, string embeddingStr)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var embedding = embeddingStr.Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var entry = new MemoryEntry
        {
            Id = id,
            Content = $"content-{id}",
            Category = category,
            Embedding = embedding,
        };
        await store.UpsertAsync([entry]);
    }

    [Given(@"I have stored entry ""(.*)"" with content ""(.*)""")]
    public async Task GivenIHaveStoredEntryWithContent(string id, string content)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var entry = new MemoryEntry
        {
            Id = id,
            Content = content,
            Embedding = [1.0f, 0.0f, 0.0f],
        };
        await store.UpsertAsync([entry]);
    }

    [When(@"I upsert a memory entry with id ""(.*)"" and content ""(.*)""")]
    public async Task WhenIUpsertAMemoryEntry(string id, string content)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var entry = new MemoryEntry
        {
            Id = id,
            Content = content,
            Embedding = [1.0f, 0.0f, 0.0f],
        };
        await store.UpsertAsync([entry]);
    }

    [When(@"I upsert entry ""(.*)"" with content ""(.*)""")]
    public async Task WhenIUpsertEntryWithContent(string id, string content)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var entry = new MemoryEntry
        {
            Id = id,
            Content = content,
            Embedding = [1.0f, 0.0f, 0.0f],
        };
        await store.UpsertAsync([entry]);
    }

    [When(@"I search with embedding \[(.+)\] for top (\d+)")]
    public async Task WhenISearchWithEmbeddingForTop(string embeddingStr, int topK)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var embedding = embeddingStr.Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var results = await store.SearchAsync(embedding, topK);
        _context.Set(results, "SearchResults");
    }

    [When(@"I search with embedding \[(.+)\] in category ""(.*)""")]
    public async Task WhenISearchWithEmbeddingInCategory(string embeddingStr, string category)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var embedding = embeddingStr.Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var results = await store.SearchAsync(embedding, categoryFilter: category);
        _context.Set(results, "SearchResults");
    }

    [When(@"I delete entry ""(.*)""")]
    public async Task WhenIDeleteEntry(string id)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        await store.DeleteAsync([id]);
    }

    [Then(@"the vector store count should be (\d+)")]
    public async Task ThenTheVectorStoreCountShouldBe(long expected)
    {
        var store = _context.Get<SqliteVectorStore>("VectorStore");
        var count = await store.CountAsync();
        count.Should().Be(expected);
    }

    [Then(@"the search results should contain ""(.*)""")]
    public void ThenTheSearchResultsShouldContain(string id)
    {
        var results = _context.Get<IReadOnlyList<MemorySearchResult>>("SearchResults");
        results.Should().Contain(r => r.Entry.Id == id);
    }

    [Then(@"the search results should not contain ""(.*)""")]
    public void ThenTheSearchResultsShouldNotContain(string id)
    {
        var results = _context.Get<IReadOnlyList<MemorySearchResult>>("SearchResults");
        results.Should().NotContain(r => r.Entry.Id == id);
    }

    public void Dispose()
    {
        _store?.Dispose();
        if (_context.TryGetValue("VecDir", out string? dir) && Directory.Exists(dir))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
