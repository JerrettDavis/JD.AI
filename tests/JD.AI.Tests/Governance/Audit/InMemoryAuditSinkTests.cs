using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;

namespace JD.AI.Tests.Governance.Audit;

public sealed class InMemoryAuditSinkTests
{
    private static AuditEvent MakeEvent(
        string action = "test",
        AuditSeverity severity = AuditSeverity.Info,
        string? sessionId = null,
        string? userId = null,
        string? resource = null,
        DateTimeOffset? timestamp = null) => new()
        {
            Action = action,
            Severity = severity,
            SessionId = sessionId,
            UserId = userId,
            Resource = resource,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        };

    [Fact]
    public async Task WriteAsync_IncrementsCount()
    {
        var sink = new InMemoryAuditSink();
        sink.Count.Should().Be(0);

        await sink.WriteAsync(MakeEvent());

        sink.Count.Should().Be(1);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEventsInReverseChronologicalOrder()
    {
        var sink = new InMemoryAuditSink();
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-2);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-1);
        var t3 = DateTimeOffset.UtcNow;

        await sink.WriteAsync(MakeEvent(action: "first", timestamp: t1));
        await sink.WriteAsync(MakeEvent(action: "second", timestamp: t2));
        await sink.WriteAsync(MakeEvent(action: "third", timestamp: t3));

        var result = await sink.QueryAsync(new AuditQuery());

        result.Events.Should().HaveCount(3);
        result.Events[0].Action.Should().Be("third");
        result.Events[1].Action.Should().Be("second");
        result.Events[2].Action.Should().Be("first");
    }

    [Fact]
    public async Task QueryAsync_FilterByAction_MatchesCaseInsensitive()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent(action: "tool.invoke"));
        await sink.WriteAsync(MakeEvent(action: "session.start"));
        await sink.WriteAsync(MakeEvent(action: "Tool.Invoke"));

        var result = await sink.QueryAsync(new AuditQuery { Action = "tool.invoke" });

        result.Events.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task QueryAsync_FilterByMinSeverity()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent(severity: AuditSeverity.Debug));
        await sink.WriteAsync(MakeEvent(severity: AuditSeverity.Info));
        await sink.WriteAsync(MakeEvent(severity: AuditSeverity.Warning));
        await sink.WriteAsync(MakeEvent(severity: AuditSeverity.Error));
        await sink.WriteAsync(MakeEvent(severity: AuditSeverity.Critical));

        var result = await sink.QueryAsync(new AuditQuery { MinSeverity = AuditSeverity.Warning });

        result.Events.Should().HaveCount(3);
        result.Events.Should().OnlyContain(e =>
            e.Severity >= AuditSeverity.Warning);
    }

    [Fact]
    public async Task QueryAsync_FilterBySessionId()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent(sessionId: "sess-1"));
        await sink.WriteAsync(MakeEvent(sessionId: "sess-2"));
        await sink.WriteAsync(MakeEvent(sessionId: "sess-1"));

        var result = await sink.QueryAsync(new AuditQuery { SessionId = "sess-1" });

        result.Events.Should().HaveCount(2);
        result.Events.Should().OnlyContain(e => e.SessionId == "sess-1");
    }

    [Fact]
    public async Task QueryAsync_FilterByUserId()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent(userId: "user-a"));
        await sink.WriteAsync(MakeEvent(userId: "user-b"));

        var result = await sink.QueryAsync(new AuditQuery { UserId = "user-a" });

        result.Events.Should().HaveCount(1);
        result.Events[0].UserId.Should().Be("user-a");
    }

    [Fact]
    public async Task QueryAsync_FilterByResource_ContainsCaseInsensitive()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent(resource: "ReadFile"));
        await sink.WriteAsync(MakeEvent(resource: "WriteFile"));
        await sink.WriteAsync(MakeEvent(resource: "read_config"));

        var result = await sink.QueryAsync(new AuditQuery { Resource = "read" });

        result.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange()
    {
        var sink = new InMemoryAuditSink();
        var now = DateTimeOffset.UtcNow;
        await sink.WriteAsync(MakeEvent(action: "old", timestamp: now.AddHours(-2)));
        await sink.WriteAsync(MakeEvent(action: "recent", timestamp: now.AddMinutes(-30)));
        await sink.WriteAsync(MakeEvent(action: "newest", timestamp: now));

        var result = await sink.QueryAsync(new AuditQuery
        {
            From = now.AddHours(-1),
            Until = now.AddMinutes(1),
        });

        result.Events.Should().HaveCount(2);
        result.Events.Select(e => e.Action).Should().Contain("recent");
        result.Events.Select(e => e.Action).Should().Contain("newest");
    }

    [Fact]
    public async Task QueryAsync_LimitAndOffset()
    {
        var sink = new InMemoryAuditSink();
        for (var i = 0; i < 10; i++)
            await sink.WriteAsync(MakeEvent(action: $"action-{i}"));

        var result = await sink.QueryAsync(new AuditQuery { Limit = 3, Offset = 2 });

        result.Events.Should().HaveCount(3);
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task QueryAsync_LimitClampedTo1000()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent());

        var result = await sink.QueryAsync(new AuditQuery { Limit = 5000 });

        // Should not throw, limit is clamped
        result.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task CircularBuffer_OverwritesOldestEvents()
    {
        var sink = new InMemoryAuditSink(capacity: 3);

        await sink.WriteAsync(MakeEvent(action: "a"));
        await sink.WriteAsync(MakeEvent(action: "b"));
        await sink.WriteAsync(MakeEvent(action: "c"));
        await sink.WriteAsync(MakeEvent(action: "d")); // Overwrites "a"

        sink.Count.Should().Be(3);

        var result = await sink.QueryAsync(new AuditQuery());
        result.Events.Should().HaveCount(3);
        result.Events.Select(e => e.Action).Should().BeEquivalentTo(["b", "c", "d"]);
    }

    [Fact]
    public async Task Name_ReturnsMemory()
    {
        var sink = new InMemoryAuditSink();
        sink.Name.Should().Be("memory");
    }

    [Fact]
    public async Task FlushAsync_DoesNotThrow()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent());

        var act = async () => await sink.FlushAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_InvalidCapacity_Throws()
    {
        var act = () => new InMemoryAuditSink(capacity: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task QueryAsync_EmptyStore_ReturnsEmptyResult()
    {
        var sink = new InMemoryAuditSink();

        var result = await sink.QueryAsync(new AuditQuery());

        result.Events.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task QueryAsync_CombinedFilters()
    {
        var sink = new InMemoryAuditSink();
        await sink.WriteAsync(MakeEvent(action: "tool.invoke", severity: AuditSeverity.Warning, sessionId: "s1"));
        await sink.WriteAsync(MakeEvent(action: "tool.invoke", severity: AuditSeverity.Debug, sessionId: "s1"));
        await sink.WriteAsync(MakeEvent(action: "tool.invoke", severity: AuditSeverity.Error, sessionId: "s2"));
        await sink.WriteAsync(MakeEvent(action: "session.start", severity: AuditSeverity.Warning, sessionId: "s1"));

        var result = await sink.QueryAsync(new AuditQuery
        {
            Action = "tool.invoke",
            MinSeverity = AuditSeverity.Warning,
            SessionId = "s1",
        });

        result.Events.Should().HaveCount(1);
        result.Events[0].Severity.Should().Be(AuditSeverity.Warning);
    }
}
