using System.Reflection;
using System.Text.Json;
using JD.AI.Core.Events;
using JD.AI.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JD.AI.Gateway.Services;

/// <summary>
/// Bridges gateway event-bus messages onto the SignalR callbacks consumed by clients.
/// </summary>
public sealed class EventHubBroadcastService(IEventBus eventBus, IHubContext<EventHub> hubContext) : IHostedService, IDisposable
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = eventBus.Subscribe(null, BroadcastAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _subscription?.Dispose();

    private async Task BroadcastAsync(GatewayEvent evt)
    {
        var clients = hubContext.Clients.Group("events");
        await clients.SendAsync(
            "ActivityEvent",
            new HubActivityEvent(evt.EventType, evt.SourceId, evt.Timestamp, GetMessage(evt.Payload)));

        if (string.Equals(evt.EventType, "channel.connected", StringComparison.OrdinalIgnoreCase))
            await clients.SendAsync("ChannelStatusChanged", evt.SourceId, true);
        else if (string.Equals(evt.EventType, "channel.disconnected", StringComparison.OrdinalIgnoreCase))
            await clients.SendAsync("ChannelStatusChanged", evt.SourceId, false);
    }

    private static string? GetMessage(object? payload)
    {
        if (payload is null)
            return null;

        if (payload is string text)
            return text;

        if (payload is JsonElement json)
        {
            if (json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;

            if (json.ValueKind == JsonValueKind.String)
                return json.GetString();

            if (json.ValueKind == JsonValueKind.Object)
            {
                if (json.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString();

                if (json.TryGetProperty("Message", out message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString();
            }

            return json.ToString();
        }

        var property = payload.GetType().GetProperty(
            "Message",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (property?.GetValue(payload) is string messageText)
            return messageText;

        return payload.ToString();
    }

    private sealed record HubActivityEvent(
        string EventType,
        string SourceId,
        DateTimeOffset Timestamp,
        string? Message);
}
