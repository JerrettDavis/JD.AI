using JD.AI.Core.Events;
using Microsoft.AspNetCore.SignalR;

namespace JD.AI.Gateway.Hubs;

/// <summary>
/// Broadcasts gateway events to connected clients in real-time.
/// </summary>
public sealed class EventHub : Hub
{
    private readonly IEventBus _eventBus;

    public EventHub(IEventBus eventBus) => _eventBus = eventBus;

    /// <summary>
    /// Streams all gateway events (or filtered by type) to the client.
    /// </summary>
    public async IAsyncEnumerable<GatewayEvent> StreamEvents(
        string? eventTypeFilter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in _eventBus.StreamAsync(eventTypeFilter, ct))
        {
            yield return evt;
        }
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "events");
        await base.OnConnectedAsync();
    }
}
