using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.Memory;
using Microsoft.Extensions.Logging;

namespace JD.AI.Tests.Memory;

public sealed class MemoryServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _projectId = "test-project-001";
    private readonly MemoryService _service;

    public MemoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-mem-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Override DataDirectories.Root so MemoryRoot returns our temp path
        DataDirectories.SetRoot(_tempDir);
        _service = new MemoryService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // ── MemoryRoot ─────────────────────────────────────────────────────────

    [Fact]
    public void MemoryRoot_ReturnsPathUnderDataDirectoriesMemoryRoot()
    {
        _service.MemoryRoot.Should().StartWith(_tempDir);
    }

    // ── GetMemoryContentAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetMemoryContentAsync_MissingFile_ReturnsEmptyString()
    {
        var content = await _service.GetMemoryContentAsync(_projectId);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemoryContentAsync_ExistingFile_ReturnsContent()
    {
        var memDir = Path.Combine(_service.MemoryRoot, _projectId);
        Directory.CreateDirectory(memDir);
        var memPath = Path.Combine(memDir, "MEMORY.md");
        await File.WriteAllTextAsync(memPath, "Hello from memory");

        var content = await _service.GetMemoryContentAsync(_projectId);

        content.Should().Be("Hello from memory");
    }

    // ── WriteMemoryContentAsync ────────────────────────────────────────────

    [Fact]
    public async Task WriteMemoryContentAsync_CreatesDirectoriesAndFile()
    {
        await _service.WriteMemoryContentAsync(_projectId, "New content");

        var memPath = Path.Combine(_service.MemoryRoot, _projectId, "MEMORY.md");
        var content = await File.ReadAllTextAsync(memPath);
        content.Should().Be("New content");
    }

    [Fact]
    public async Task WriteMemoryContentAsync_OverwritesExisting()
    {
        await _service.WriteMemoryContentAsync(_projectId, "Original");
        await _service.WriteMemoryContentAsync(_projectId, "Updated");

        var content = await _service.GetMemoryContentAsync(_projectId);
        content.Should().Be("Updated");
    }

    // ── AppendToDailyLogAsync ─────────────────────────────────────────────

    [Fact]
    public async Task AppendToDailyLogAsync_CreatesFileAndAppendsLine()
    {
        var entry = "Turn 1: What is the weather?";
        await _service.AppendToDailyLogAsync(_projectId, entry);

        var entries = await GetDailyLogEntries();
        entries.Should().Contain(entry);
    }

    [Fact]
    public async Task AppendToDailyLogAsync_AppendsMultipleLines()
    {
        await _service.AppendToDailyLogAsync(_projectId, "Entry A");
        await _service.AppendToDailyLogAsync(_projectId, "Entry B");

        var entries = await GetDailyLogEntries();
        entries.Should().HaveCount(2);
    }

    // ── GetDailyLogAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDailyLogAsync_MissingFile_ReturnsNull()
    {
        var result = await _service.GetDailyLogAsync(_projectId, DateTimeOffset.UtcNow.AddDays(-7));
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDailyLogAsync_ExistingFile_ReturnsContent()
    {
        var date = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var memDir = Path.Combine(_service.MemoryRoot, _projectId, "memory", "2025/01");
        Directory.CreateDirectory(memDir);
        var file = Path.Combine(memDir, "2025-01-15.md");
        await File.WriteAllTextAsync(file, "Test log entry");

        var result = await _service.GetDailyLogAsync(_projectId, date);

        result.Should().Contain("Test log entry");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<List<string>> GetDailyLogEntries()
    {
        var today = DateTimeOffset.UtcNow;
        var file = Path.Combine(_service.MemoryRoot, _projectId, "memory",
            today.ToString("yyyy/MM", System.Globalization.CultureInfo.InvariantCulture),
            $"{today:yyyy-MM-dd}.md");
        if (!File.Exists(file)) return [];
        var lines = await File.ReadAllLinesAsync(file);
        return lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
    }
}
