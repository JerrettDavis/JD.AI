using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JD.AI.Core.Events;
using JD.AI.Gateway.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Gateway.Tests;

public sealed class AuditControllerTests
{
    private readonly IEventBus _eventBus;
    private readonly AuditController _controller;

    public AuditControllerTests()
    {
        _eventBus = Substitute.For<IEventBus>();
        _controller = new AuditController(_eventBus);
    }

    [Fact]
    public void Constructor_WithNullEventBus_Throws()
    {
        var act = () => new AuditController(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── GetAuditEvents ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditEvents_WithDefaultLimit_ReturnsEvents()
    {
        var events = new GatewayEvent[]
        {
            new GatewayEvent("tool.invoke", "agent-1", DateTimeOffset.UtcNow) { Id = "event-1" },
            new GatewayEvent("session.create", "agent-2", DateTimeOffset.UtcNow.AddSeconds(-1)) { Id = "event-2" },
        };

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 500, ct: CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().BeInDescendingOrder(e => e.Timestamp);
    }

    [Fact]
    public async Task GetAuditEvents_WithLimit_ClampsBetween1And2000()
    {
        var events = Array.Empty<GatewayEvent>();
        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        // Test lower bound clamping (0 -> 1)
        await _controller.GetAuditEvents(limit: 0, ct: CancellationToken.None);
        await _eventBus.Received(1).GetEvents(Arg.Any<CancellationToken>());

        // Test upper bound clamping (5000 -> 2000)
        _eventBus.ClearReceivedCalls();
        await _controller.GetAuditEvents(limit: 5000, ct: CancellationToken.None);
        await _eventBus.Received(1).GetEvents(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAuditEvents_WithManyEvents_TakesSpecifiedLimit()
    {
        var events = Enumerable.Range(0, 1000)
            .Select(i => new GatewayEvent("test", "agent", DateTimeOffset.UtcNow.AddSeconds(-i)) { Id = $"event-{i}" })
            .ToArray();

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 100, ct: CancellationToken.None);

        result.Should().HaveCount(100);
    }

    [Fact]
    public async Task GetAuditEvents_ReturnsEventsInDescendingTimestampOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new GatewayEvent[]
        {
            new GatewayEvent("type1", "a", now.AddSeconds(-100)) { Id = "old" },
            new GatewayEvent("type2", "b", now) { Id = "newer" },
            new GatewayEvent("type3", "c", now.AddSeconds(-50)) { Id = "middle" },
        };

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 500, ct: CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Id.Should().Be("newer");
        result[1].Id.Should().Be("middle");
        result[2].Id.Should().Be("old");
    }

    [Fact]
    public async Task GetAuditEvents_MapsToDtoCorrectly()
    {
        var payload = new { action = "test" };
        var events = new GatewayEvent[]
        {
            new GatewayEvent("session.create", "agent-xyz", DateTimeOffset.UtcNow, payload) { Id = "event-123" },
        };

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 500, ct: CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("event-123");
        result[0].AgentId.Should().Be("agent-xyz");
        result[0].EventType.Should().Be("session.create");
        result[0].Message.Should().Be("session.create");
        result[0].Payload.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public async Task GetAuditEvents_WithNullSourceId_MapsToEmptyString()
    {
        var events = new GatewayEvent[]
        {
            new GatewayEvent("test.event", null, DateTimeOffset.UtcNow) { Id = "event-1" },
        };

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 500, ct: CancellationToken.None);

        result[0].AgentId.Should().Be("");
    }

    [Fact]
    public async Task GetAuditEvents_EventBusThrowsNotSupported_ReturnsEmptyList()
    {
        _eventBus.GetEvents(Arg.Any<CancellationToken>())
            .Throws(new NotSupportedException("In-memory event bus not available"));

        var result = await _controller.GetAuditEvents(limit: 500, ct: CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAuditEvents_PassesCancellationToken()
    {
        var cts = new CancellationTokenSource();
        var events = Array.Empty<GatewayEvent>();
        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        await _controller.GetAuditEvents(limit: 500, ct: cts.Token);

        await _eventBus.Received(1).GetEvents(cts.Token);
    }

    [Fact]
    public async Task GetAuditEvents_ReturnsIReadOnlyList()
    {
        var events = new GatewayEvent[]
        {
            new GatewayEvent("test", "agent", DateTimeOffset.UtcNow) { Id = "event-1" },
        };

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 500, ct: CancellationToken.None);

        result.Should().BeAssignableTo<IReadOnlyList<AuditEventDto>>();
    }

    [Fact]
    public async Task GetAuditEvents_LimitExceedsMax_ClampsTo2000()
    {
        var events = Enumerable.Range(0, 3000)
            .Select(i => new GatewayEvent("test", "agent", DateTimeOffset.UtcNow.AddSeconds(-i)) { Id = $"event-{i}" })
            .ToArray();

        _eventBus.GetEvents(Arg.Any<CancellationToken>()).Returns(Task.FromResult(events));

        var result = await _controller.GetAuditEvents(limit: 3000, ct: CancellationToken.None);

        result.Should().HaveCount(2000);
    }

    // ── AuditEventDto ──────────────────────────────────────────────────────

    [Fact]
    public void AuditEventDto_CanBeConstructed()
    {
        var dto = new AuditEventDto
        {
            Id = "test-id",
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "agent-1",
            EventType = "test.event",
            Message = "Test message",
            Payload = null,
        };

        dto.Id.Should().Be("test-id");
        dto.AgentId.Should().Be("agent-1");
        dto.EventType.Should().Be("test.event");
        dto.Message.Should().Be("Test message");
        dto.Payload.Should().BeNull();
    }

    [Fact]
    public void AuditEventDto_WithPayload()
    {
        var payload = new { key = "value" };
        var dto = new AuditEventDto
        {
            Id = "test-id",
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = "agent-1",
            EventType = "test.event",
            Message = "Message",
            Payload = payload,
        };

        dto.Payload.Should().Be(payload);
    }
}
