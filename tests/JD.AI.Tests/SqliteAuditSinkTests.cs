using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using Xunit;

namespace JD.AI.Tests;

public sealed class SqliteAuditSinkTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteAuditSink _sink;

    public SqliteAuditSinkTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.db");
        _sink = new SqliteAuditSink(_dbPath);
    }

    public async ValueTask DisposeAsync()
    {
        await _sink.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    // ── write and count ──────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_SingleEvent_CountIncreases()
    {
        var evt = MakeEvent("ToolCall");
        await _sink.WriteAsync(evt);
        Assert.Equal(1, _sink.Count);
    }

    [Fact]
    public async Task WriteAsync_DuplicateEventId_NotDuplicated()
    {
        var evt = MakeEvent("ToolCall");
        await _sink.WriteAsync(evt);
        await _sink.WriteAsync(evt); // same event id
        Assert.Equal(1, _sink.Count);
    }

    [Fact]
    public async Task WriteAsync_MultipleEvents_AllPersisted()
    {
        for (var i = 0; i < 5; i++)
            await _sink.WriteAsync(MakeEvent("Action" + i));

        Assert.Equal(5, _sink.Count);
    }

    // ── query — no filter ────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_NoFilter_ReturnsAllEvents()
    {
        await _sink.WriteAsync(MakeEvent("A"));
        await _sink.WriteAsync(MakeEvent("B"));

        var result = await _sink.QueryAsync(new AuditQuery { Limit = 100 });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Events.Count);
    }

    // ── query — action filter ────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_FilterByAction_ReturnsMatchingOnly()
    {
        await _sink.WriteAsync(MakeEvent("ToolCall"));
        await _sink.WriteAsync(MakeEvent("ProviderCall"));
        await _sink.WriteAsync(MakeEvent("ToolCall"));

        var result = await _sink.QueryAsync(new AuditQuery { Action = "ToolCall" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Events, e => Assert.Equal("ToolCall", e.Action));
    }

    [Fact]
    public async Task QueryAsync_ActionIsCaseInsensitive()
    {
        await _sink.WriteAsync(MakeEvent("ToolCall"));

        var result = await _sink.QueryAsync(new AuditQuery { Action = "toolcall" });
        Assert.Equal(1, result.TotalCount);
    }

    // ── query — severity filter ──────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_FilterByMinSeverity_ExcludesLower()
    {
        await _sink.WriteAsync(MakeEvent("A", AuditSeverity.Info));
        await _sink.WriteAsync(MakeEvent("B", AuditSeverity.Warning));
        await _sink.WriteAsync(MakeEvent("C", AuditSeverity.Error));

        var result = await _sink.QueryAsync(new AuditQuery { MinSeverity = AuditSeverity.Warning });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Events, e => Assert.True(e.Severity >= AuditSeverity.Warning));
    }

    // ── query — user/session filters ─────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_FilterByUserId_ReturnsMatchingOnly()
    {
        await _sink.WriteAsync(MakeEvent("A", userId: "alice"));
        await _sink.WriteAsync(MakeEvent("B", userId: "bob"));

        var result = await _sink.QueryAsync(new AuditQuery { UserId = "alice" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("alice", result.Events[0].UserId);
    }

    [Fact]
    public async Task QueryAsync_FilterBySessionId_ReturnsMatchingOnly()
    {
        var sid = "session-abc";
        await _sink.WriteAsync(MakeEvent("A", sessionId: sid));
        await _sink.WriteAsync(MakeEvent("B", sessionId: "other"));

        var result = await _sink.QueryAsync(new AuditQuery { SessionId = sid });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(sid, result.Events[0].SessionId);
    }

    // ── query — time filters ─────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_FilterByFrom_ExcludesOlderEvents()
    {
        var now = DateTimeOffset.UtcNow;
        await _sink.WriteAsync(MakeEvent("Old", timestamp: now.AddHours(-2)));
        await _sink.WriteAsync(MakeEvent("New", timestamp: now));

        var result = await _sink.QueryAsync(new AuditQuery { From = now.AddHours(-1) });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("New", result.Events[0].Action);
    }

    // ── query — pagination ───────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_Pagination_ReturnsCorrectPage()
    {
        for (var i = 0; i < 10; i++)
            await _sink.WriteAsync(MakeEvent("Action" + i));

        var page1 = await _sink.QueryAsync(new AuditQuery { Limit = 5, Offset = 0 });
        var page2 = await _sink.QueryAsync(new AuditQuery { Limit = 5, Offset = 5 });

        Assert.Equal(5, page1.Events.Count);
        Assert.Equal(5, page2.Events.Count);
        Assert.Equal(10, page1.TotalCount);
        // No overlap
        var ids1 = page1.Events.Select(e => e.EventId).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(page2.Events, e => ids1.Contains(e.EventId));
    }

    // ── query — newest first ordering ────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_OrderIsNewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await _sink.WriteAsync(MakeEvent("First", timestamp: now.AddMinutes(-10)));
        await _sink.WriteAsync(MakeEvent("Second", timestamp: now.AddMinutes(-5)));
        await _sink.WriteAsync(MakeEvent("Third", timestamp: now));

        var result = await _sink.QueryAsync(new AuditQuery());

        Assert.Equal("Third", result.Events[0].Action);
        Assert.Equal("First", result.Events[^1].Action);
    }

    // ── IAuditSink interface ─────────────────────────────────────────────────

    [Fact]
    public void Name_IsCorrect()
    {
        Assert.Equal("sqlite", _sink.Name);
    }

    [Fact]
    public async Task FlushAsync_DoesNotThrow()
    {
        await _sink.FlushAsync(); // should be no-op
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static AuditEvent MakeEvent(
        string action,
        AuditSeverity severity = AuditSeverity.Info,
        string? userId = null,
        string? sessionId = null,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            Action = action,
            Severity = severity,
            UserId = userId,
            SessionId = sessionId,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        };
}
