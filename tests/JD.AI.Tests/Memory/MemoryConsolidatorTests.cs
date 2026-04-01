using FluentAssertions;
using JD.AI.Core.Config;
using JD.AI.Core.Memory;

namespace JD.AI.Tests.Memory;

/// <summary>
/// Tests for <see cref="MemoryConsolidator"/> and <see cref="MemoryConsolidatorTests"/>
/// consolidation logic via <c>ConsolidateProjectAsync</c>.
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

        // Override DataDirectories so MemoryRoot resolves to our temp dir
        DataDirectories.SetRoot(_tempDir);
        _memoryService = new MemoryService();
        _consolidator = new MemoryConsolidator(_memoryService, TimeSpan.FromHours(1));
    }

    public void Dispose()
    {
        _consolidator.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    // ── ConsolidateProjectAsync ────────────────────────────────────────────

    [Fact]
    public async Task ConsolidateProjectAsync_CreatesMemoryMd_FromDailyLogs()
    {
        var projectId = "proj-1";
        await _memoryService.AppendToDailyLogAsync(projectId, "Turn 1: What is the weather?");
        await _memoryService.AppendToDailyLogAsync(projectId, "Turn 2: Tell me about London");
        await _memoryService.AppendToDailyLogAsync(projectId, "Turn 3: Thanks!");

        await _consolidator.ConsolidateProjectAsync(projectId, CancellationToken.None);

        var mem = await _memoryService.GetMemoryContentAsync(projectId);
        mem.Should().NotBeEmpty();
        mem.Should().Contain("Turn 1");
        mem.Should().Contain("Turn 2");
        mem.Should().Contain("Turn 3");
    }

    [Fact]
    public async Task ConsolidateProjectAsync_EmptyProject_WritesNoRecentTurns()
    {
        var projectId = "proj-empty";

        await _consolidator.ConsolidateProjectAsync(projectId, CancellationToken.None);

        var mem = await _memoryService.GetMemoryContentAsync(projectId);
        mem.Should().Contain("No turns recorded");
    }

    [Fact]
    public async Task ConsolidateProjectAsync_Cancellation_ThrowsOperationCancelled()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _consolidator.ConsolidateProjectAsync("proj", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConsolidateProjectAsync_NonExistentProject_DoesNotThrow()
    {
        var act = () => _consolidator.ConsolidateProjectAsync(
            "nonexistent-project", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // ── ConsolidateAsync (sweep) ───────────────────────────────────────────

    [Fact]
    public async Task ConsolidateAsync_SweepsAllProjects()
    {
        await _memoryService.AppendToDailyLogAsync("proj-A", "Entry A1");
        await _memoryService.AppendToDailyLogAsync("proj-B", "Entry B1");

        await _consolidator.ConsolidateAsync(CancellationToken.None);

        (await _memoryService.GetMemoryContentAsync("proj-A")).Should().Contain("Entry A1");
        (await _memoryService.GetMemoryContentAsync("proj-B")).Should().Contain("Entry B1");
    }

    [Fact]
    public async Task ConsolidateAsync_Cancellation_ThrowsOperationCancelled()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _consolidator.ConsolidateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Constructor ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithTimeSpan_DoesNotThrow()
    {
        var custom = new MemoryConsolidator(_memoryService, TimeSpan.FromMinutes(30));
        custom.Should().NotBeNull();
        custom.Dispose();
    }
}
