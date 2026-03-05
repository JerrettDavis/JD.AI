using FluentAssertions;
using JD.AI.Core.Governance;

namespace JD.AI.Tests.Governance;

public sealed class PolicyVersionHistoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _historyPath;

    public PolicyVersionHistoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-pvh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _historyPath = Path.Combine(_tempDir, "policy-history.jsonl");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public void Record_CreatesHistoryFile()
    {
        var history = new PolicyVersionHistory(_historyPath);

        history.Record("test-policy", "content: here");

        File.Exists(_historyPath).Should().BeTrue();
    }

    [Fact]
    public void GetHistory_AfterRecord_ReturnsEntry()
    {
        var history = new PolicyVersionHistory(_historyPath);
        history.Record("test-policy", "content: here");

        var entries = history.GetHistory();

        entries.Should().HaveCount(1);
        entries[0].PolicyName.Should().Be("test-policy");
        entries[0].ContentHash.Should().NotBeNullOrEmpty();
        entries[0].ContentLength.Should().Be("content: here".Length);
        entries[0].Author.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetHistory_FiltersByPolicyName()
    {
        var history = new PolicyVersionHistory(_historyPath);
        history.Record("policy-a", "a content");
        history.Record("policy-b", "b content");
        history.Record("policy-a", "a updated");

        var entriesA = history.GetHistory("policy-a");
        var entriesB = history.GetHistory("policy-b");

        entriesA.Should().HaveCount(2);
        entriesB.Should().HaveCount(1);
    }

    [Fact]
    public void GetHistory_ReturnsNewestFirst()
    {
        var history = new PolicyVersionHistory(_historyPath);
        history.Record("p", "v1");
        history.Record("p", "v2");

        var entries = history.GetHistory();

        entries[0].Timestamp.Should().BeOnOrAfter(entries[1].Timestamp);
    }

    [Fact]
    public void GetHistory_RespectsLimit()
    {
        var history = new PolicyVersionHistory(_historyPath);
        for (var i = 0; i < 10; i++)
            history.Record("p", $"content-{i}");

        var entries = history.GetHistory(limit: 3);

        entries.Should().HaveCount(3);
    }

    [Fact]
    public void GetHistory_NoFile_ReturnsEmpty()
    {
        var noFilePath = Path.Combine(_tempDir, "nonexistent.jsonl");
        var history = new PolicyVersionHistory(noFilePath);

        var entries = history.GetHistory();

        entries.Should().BeEmpty();
    }

    [Fact]
    public void HasChanged_NewPolicy_ReturnsTrue()
    {
        var history = new PolicyVersionHistory(_historyPath);

        history.HasChanged("new-policy", "content").Should().BeTrue();
    }

    [Fact]
    public void HasChanged_SameContent_ReturnsFalse()
    {
        var history = new PolicyVersionHistory(_historyPath);
        history.Record("p", "same content");

        history.HasChanged("p", "same content").Should().BeFalse();
    }

    [Fact]
    public void HasChanged_DifferentContent_ReturnsTrue()
    {
        var history = new PolicyVersionHistory(_historyPath);
        history.Record("p", "original content");

        history.HasChanged("p", "modified content").Should().BeTrue();
    }

    [Fact]
    public void Record_SameContentTwice_ProducesSameHash()
    {
        var history = new PolicyVersionHistory(_historyPath);
        history.Record("p", "same content");
        history.Record("p", "same content");

        var entries = history.GetHistory("p");

        entries[0].ContentHash.Should().Be(entries[1].ContentHash);
    }
}
