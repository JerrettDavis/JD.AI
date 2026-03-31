using JD.AI.Core.Events;
using Microsoft.AspNetCore.Mvc;

namespace JD.AI.Gateway.Endpoints;

/// <summary>
/// Exposes historical gateway events via REST for the Dashboard Logs page.
/// Events are served from the in-memory event buffer maintained by <see cref="InMemoryEventBus"/>.
/// </summary>
[ApiController]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    public AuditController([FromServices] IEventBus eventBus)
    {
        // IEventBus is registered as InMemoryEventBus in DI.
        // InMemoryEventBus is internal but we're injecting via the public interface.
        ArgumentNullException.ThrowIfNull(eventBus);
        _eventBus = eventBus;
    }

    private readonly IEventBus _eventBus;

    /// <summary>
    /// Returns the most recent gateway events, newest first.
    /// </summary>
    /// <param name="limit">Maximum number of events to return (default: 500, max: 2000).</param>
    [HttpGet]
    public IReadOnlyList<AuditEventDto> GetAuditEvents([FromQuery] int limit = 500)
    {
        var clampedLimit = Math.Clamp(limit, 1, 2000);
        // GetEvents() is InMemoryEventBus-specific — cast is safe because
        // InMemoryEventBus is registered as IEventBus singleton in DI.
        if (_eventBus is not InMemoryEventBus eb)
            return [];

        return eb.GetEvents()
            .OrderByDescending(e => e.Timestamp)
            .Take(clampedLimit)
            .Select(ToDto)
            .ToList();
    }

    private static AuditEventDto ToDto(GatewayEvent e) => new()
    {
        Id = e.Id.ToString(),
        Timestamp = e.Timestamp,
        Level = e.Level.ToString(),
        AgentId = e.SourceId ?? "",
        EventType = e.EventType,
        Message = e.Message ?? e.EventType,
        Payload = e.Payload
    };
}

public sealed class AuditEventDto
{
    public required string Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required string Level { get; init; }
    public required string AgentId { get; init; }
    public required string EventType { get; init; }
    public required string Message { get; init; }
    public string? Payload { get; init; }
}
