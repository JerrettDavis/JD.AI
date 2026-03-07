using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace JD.AI.Core.Events;

/// <summary>
///     Distributed event bus backed by Redis Pub/Sub.
///     Publishes events to a Redis channel so all connected nodes
///     receive them; local subscriptions are dispatched in-process.
/// </summary>
public sealed class RedisEventBus : IEventBus, IAsyncDisposable, IDisposable
{
    private const string ChannelPrefix = "jdai:events:";
    private const string AllEventsChannel = ChannelPrefix + "*";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Lock _lock = new();
    private readonly ILogger<RedisEventBus> _logger;
    private readonly string _nodeId = Guid.NewGuid().ToString("N")[..8];

    private readonly IConnectionMultiplexer _redis;
    private readonly List<Subscription> _subscriptions = [];
    private bool _disposed;

    public RedisEventBus(IConnectionMultiplexer redis, ILogger<RedisEventBus> logger)
    {
        _redis = redis;
        _logger = logger;
        SubscribeToRedis();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.UnsubscribeAllAsync().ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing from Redis during dispose");
        }

        lock (_lock)
        {
            foreach (var sub in _subscriptions)
                sub.MarkRemoved();
            _subscriptions.Clear();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async Task PublishAsync(GatewayEvent evt, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var envelope = new EventEnvelope(evt.Id, evt.EventType, evt.SourceId, evt.Timestamp, evt.Payload, _nodeId);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var channel = new RedisChannel(ChannelPrefix + evt.EventType, RedisChannel.PatternMode.Literal);

        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.PublishAsync(channel, json).ConfigureAwait(false);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis publish failed for {EventType}, dispatching locally", evt.EventType);
            await DispatchLocalAsync(evt).ConfigureAwait(false);
        }
    }

    public IDisposable Subscribe(string? eventTypeFilter, Func<GatewayEvent, Task> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sub = new Subscription(eventTypeFilter, handler, this);
        lock (_lock) _subscriptions.Add(sub);
        return sub;
    }

    public async IAsyncEnumerable<GatewayEvent> StreamAsync(
        string? eventTypeFilter,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<GatewayEvent>(
            new UnboundedChannelOptions { SingleReader = true });

        using var sub = Subscribe(eventTypeFilter,
            async evt => { await channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false); });

        await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false)) yield return evt;
    }

    private void SubscribeToRedis()
    {
        var subscriber = _redis.GetSubscriber();
        subscriber.Subscribe(
            new RedisChannel(AllEventsChannel, RedisChannel.PatternMode.Pattern),
            (_, message) => OnRedisMessage(message));
    }

    private void OnRedisMessage(RedisValue message)
    {
        if (message.IsNullOrEmpty) return;

        try
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.ToString(), JsonOptions);
            if (envelope is null) return;

            var evt = new GatewayEvent(envelope.EventType, envelope.SourceId, envelope.Timestamp, envelope.Payload)
            {
                Id = envelope.Id
            };

            _ = DispatchLocalAsync(evt);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Redis event message");
        }
    }

    private Task DispatchLocalAsync(GatewayEvent evt)
    {
        List<Subscription> targets;
        lock (_lock)
            targets = _subscriptions.Where(s =>
                    s.Filter is null || string.Equals(s.Filter, evt.EventType, StringComparison.Ordinal)).
                ToList();

        return Task.WhenAll(targets.Select(s => s.Dispatch(evt)));
    }

    private void Remove(Subscription sub)
    {
        lock (_lock) _subscriptions.Remove(sub);
    }

    private sealed record EventEnvelope(
        string Id,
        string EventType,
        string SourceId,
        DateTimeOffset Timestamp,
        object? Payload,
        string NodeId);

    private sealed class Subscription(
        string? filter,
        Func<GatewayEvent, Task> handler,
        RedisEventBus bus) : IDisposable
    {
        private bool _removed;
        public string? Filter => filter;

        public void Dispose()
        {
            _removed = true;
            bus.Remove(this);
        }

        public Task Dispatch(GatewayEvent evt) =>
            _removed ? Task.CompletedTask : handler(evt);

        public void MarkRemoved() => _removed = true;
    }
}
