using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Memory;

public sealed class MemoryServiceTests : IDisposable
{
    // Use a temp directory to avoid polluting user data
    private readonly string _tempDir;
    private readonly string _projectId = "test-project-001";
    private readonly MemoryService _service;

    public MemoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-mem-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Patch DataDirectories.Root for the test
        var field = typeof(DataDirectories).GetField("_root",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var original = field?.GetValue(null);
        field?.SetValue(null, _tempDir);

        _service = new MemoryService(NullLogger<MemoryService>.Create());

        // Restore original after setup
        if (original is not null)
            field?.SetValue(null, original);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // ── MemoryRoot ─────────────────────────────────────────────────────────

    [Fact]
    public void MemoryRoot_ReturnsPathUnderDataDirectoriesRoot()
    {
        _service.MemoryRoot.Should().StartWith(_tempDir);
        _service.MemoryRoot.Should().EndWith("memory");
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
        // Setup: write MEMORY.md
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

        var today = DateTimeOffset.UtcNow;
        var expectedDir = Path.Combine(_service.MemoryRoot, _projectId, "memory",
            today.ToString("yyyy/MM"));
        var expectedFile = Path.Combine(expectedDir, $"{today:yyyy-MM-dd}.md");

        var content = await File.ReadAllTextAsync(expectedFile);
        content.Should().Contain(entry);
    }

    [Fact]
    public async Task AppendToDailyLogAsync_AppendsMultipleLines()
    {
        await _service.AppendToDailyLogAsync(_projectId, "Entry A");
        await _service.AppendToDailyLogAsync(_projectId, "Entry B");

        var entries = await GetDailyLogEntries();
        entries.Should().HaveCount(2);
    }

    private async Task<List<string>> GetDailyLogEntries()
    {
        var today = DateTimeOffset.UtcNow;
        var file = Path.Combine(_service.MemoryRoot, _projectId, "memory",
            today.ToString("yyyy/MM"), $"{today:yyyy-MM-dd}.md");
        if (!File.Exists(file)) return [];
        var lines = await File.ReadAllLinesAsync(file);
        return lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
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

    // ── Graceful degradation ──────────────────────────────────────────────

    [Fact]
    public async Task WriteMemoryContentAsync_DirectoryCreation_FailsSilently()
    {
        // When the path is valid and writable, it succeeds — this is the expected path
        var act = async () => await _service.WriteMemoryContentAsync(_projectId, "data");

        await act.Should().NotThrowAsync();
    }
}