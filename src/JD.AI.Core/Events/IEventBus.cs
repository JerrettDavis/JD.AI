namespace JD.AI.Core.Events;

/// <summary>
/// Represents a platform event raised by agents, channels, or the gateway.
/// </summary>
public record GatewayEvent(
    string EventType,
    string SourceId,
    DateTimeOffset Timestamp,
    object? Payload = null)
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// Publish/subscribe event bus for cross-cutting gateway events.
/// </summary>
public interface IEventBus
{
    /// <summary>Publishes an event to all subscribers.</summary>
    Task PublishAsync(GatewayEvent evt, CancellationToken ct = default);

    /// <summary>Subscribes to events matching the given filter.</summary>
    IDisposable Subscribe(string? eventTypeFilter, Func<GatewayEvent, Task> handler);

    /// <summary>Returns an async enumerable of events for streaming scenarios.</summary>
    IAsyncEnumerable<GatewayEvent> StreamAsync(string? eventTypeFilter, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all currently buffered events, oldest first.
    /// Only implemented by in-process transports; distributed transports throw.
    /// </summary>
    Task<GatewayEvent[]> GetEvents(CancellationToken ct = default) =>
        throw new NotSupportedException("GetEvents is only supported by InMemoryEventBus");
}
