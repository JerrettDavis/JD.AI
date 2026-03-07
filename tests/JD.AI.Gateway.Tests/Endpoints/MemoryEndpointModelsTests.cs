using FluentAssertions;
using JD.AI.Gateway.Endpoints;

namespace JD.AI.Gateway.Tests.Endpoints;

public sealed class MemoryEndpointModelsTests
{
    // ── IndexRequest record ───────────────────────────────────────────────

    [Fact]
    public void IndexRequest_Construction()
    {
        var embedding = new float[] { 1f, 2f, 3f };
        var req = new IndexRequest("doc-1", "Hello world", embedding);

        req.Id.Should().Be("doc-1");
        req.Text.Should().Be("Hello world");
        req.Embedding.Should().BeEquivalentTo(embedding);
        req.Source.Should().BeNull();
        req.Category.Should().BeNull();
        req.Metadata.Should().BeNull();
    }

    [Fact]
    public void IndexRequest_WithOptionalFields()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "value" };
        var req = new IndexRequest("doc-1", "Hello", [1f], "readme", "docs", metadata);

        req.Source.Should().Be("readme");
        req.Category.Should().Be("docs");
        req.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void IndexRequest_RecordEquality()
    {
        var emb = new float[] { 1f };
        var a = new IndexRequest("doc-1", "text", emb);
        var b = new IndexRequest("doc-1", "text", emb);
        a.Should().Be(b);
    }

    // ── SearchRequest record ──────────────────────────────────────────────

    [Fact]
    public void SearchRequest_DefaultTopK()
    {
        var req = new SearchRequest([1f, 0f, 0f]);

        req.TopK.Should().Be(5);
        req.CategoryFilter.Should().BeNull();
    }

    [Fact]
    public void SearchRequest_CustomValues()
    {
        var req = new SearchRequest([1f], TopK: 10, CategoryFilter: "code");

        req.TopK.Should().Be(10);
        req.CategoryFilter.Should().Be("code");
    }

    [Fact]
    public void SearchRequest_RecordEquality()
    {
        var emb = new float[] { 1f };
        var a = new SearchRequest(emb, 5, null);
        var b = new SearchRequest(emb, 5, null);
        a.Should().Be(b);
    }
}
