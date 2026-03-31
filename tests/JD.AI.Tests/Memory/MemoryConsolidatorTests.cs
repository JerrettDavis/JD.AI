using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Memory;

/// <summary>
/// Tests for <see cref="MemoryConsolidator"/> background consolidation
/// logic and scheduling.
/// </summary>
public sealed class MemoryConsolidatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MemoryService _memoryService;
    private readonly MemoryConsolidator _consolidator;

    public MemoryConsolidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"jdai-consolidation-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Override DataDirectories.Root for testing
        var field = typeof(DataDirectories).GetField("_root",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var original = field?.GetValue(null);
        field?.SetValue(null, _tempDir);

        _memoryService = new MemoryService(NullLogger<MemoryService>.Instance);
        _consolidator = new MemoryConsolidator(_memoryService,
            consolidateIntervalHours: 1,
            logger: NullLogger<MemoryConsolidator>.Instance);

        if (original is not null)
            field?.SetValue(null, original);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // ── Consolidation creates MEMORY.md from daily logs ───────────────────

    [Fact]
    public async Task ConsolidateAsync_ReadsDailyLogs_AndWritesMemoryMd()
    {
        var projectId = "proj-consolidation-1";

        // Write 3 daily log entries
        await _memoryService.AppendToDailyLogAsync(projectId, "Turn 1: What is the weather?");
        await _memoryService.AppendToDailyLogAsync(projectId, "Turn 2: Tell me about London");
        await _memoryService.AppendToDailyLogAsync(projectId, "Turn 3: Thanks!");

        // Run consolidation
        await _consolidator.ConsolidateAsync(new[] { projectId }, CancellationToken.None);

        // MEMORY.md should now exist
        var mem = await _memoryService.GetMemoryContentAsync(projectId);
        mem.Should().NotBeEmpty();
        // Memory file should reference the project
        var memPath = Path.Combine(_memoryService.MemoryRoot, projectId, "MEMORY.md");
        File.Exists(memPath).Should().BeTrue();
    }

    [Fact]
    public async Task ConsolidateAsync_MultipleProjects_ProcessesAll()
    {
        await _memoryService.AppendToDailyLogAsync("proj-A", "Entry A1");
        await _memoryService.AppendToDailyLogAsync("proj-B", "Entry B1");

        var projects = new[] { "proj-A", "proj-B" };
        await _consolidator.ConsolidateAsync(projects, CancellationToken.None);

        (await _memoryService.GetMemoryContentAsync("proj-A")).Should().NotBeEmpty();
        (await _memoryService.GetMemoryContentAsync("proj-B")).Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConsolidateAsync_EmptyProject_NoMemoryMdCreated()
    {
        var projectId = "proj-empty";
        // No daily logs written

        await _consolidator.ConsolidateAsync(new[] { projectId }, CancellationToken.None);

        var memPath = Path.Combine(_memoryService.MemoryRoot, projectId, "MEMORY.md");
        File.Exists(memPath).Should().BeFalse();
    }

    [Fact]
    public async Task ConsolidateAsync_Cancellation_ThrowsOperationCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _consolidator.ConsolidateAsync(new[] { "proj" }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConsolidateAsync_MissingProject_DoesNotThrow()
    {
        var act = async () => await _consolidator.ConsolidateAsync(
            new[] { "nonexistent-project" }, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void IntervalIsConfigurable()
    {
        var custom = new MemoryConsolidator(_memoryService,
            consolidateIntervalHours: 4,
            logger: NullLogger<MemoryConsolidator>.Instance);

        custom.Should().NotBeNull();
    }
}