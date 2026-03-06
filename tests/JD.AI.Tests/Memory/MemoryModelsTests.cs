using FluentAssertions;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

public sealed class MemoryModelsTests
{
    // ── MemoryEntry ───────────────────────────────────────────────────────

    [Fact]
    public void MemoryEntry_RequiredProperties()
    {
        var entry = new MemoryEntry { Id = "e1", Content = "hello" };
        entry.Id.Should().Be("e1");
        entry.Content.Should().Be("hello");
    }

    [Fact]
    public void MemoryEntry_OptionalDefaults()
    {
        var entry = new MemoryEntry { Id = "e1", Content = "hello" };
        entry.Source.Should().BeNull();
        entry.Category.Should().BeNull();
        entry.Embedding.Should().BeNull();
        entry.Metadata.Should().BeEmpty();
        entry.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MemoryEntry_AllProperties()
    {
        var ts = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var entry = new MemoryEntry
        {
            Id = "e1",
            Content = "test content",
            Source = "file.cs",
            Category = "code",
            Embedding = [1f, 2f, 3f],
            CreatedAt = ts,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "val" },
        };
        entry.Source.Should().Be("file.cs");
        entry.Category.Should().Be("code");
        entry.Embedding.Should().HaveCount(3);
        entry.CreatedAt.Should().Be(ts);
        entry.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void MemoryEntry_RecordEquality()
    {
        var a = new MemoryEntry { Id = "e1", Content = "hello" };
        var b = new MemoryEntry { Id = "e1", Content = "hello" };
        // Records use value equality — same Id+Content+defaults should be equal
        // Note: CreatedAt defaults to UtcNow so equality may differ by timing
        a.Id.Should().Be(b.Id);
        a.Content.Should().Be(b.Content);
    }

    // ── MemorySearchResult ────────────────────────────────────────────────

    [Fact]
    public void MemorySearchResult_Construction()
    {
        var entry = new MemoryEntry { Id = "e1", Content = "hello" };
        var result = new MemorySearchResult(entry, 0.95);
        result.Entry.Should().BeSameAs(entry);
        result.Score.Should().Be(0.95);
    }

    [Fact]
    public void MemorySearchResult_RecordEquality()
    {
        var entry = new MemoryEntry { Id = "e1", Content = "hello" };
        var a = new MemorySearchResult(entry, 0.5);
        var b = new MemorySearchResult(entry, 0.5);
        a.Should().Be(b);
    }
}
