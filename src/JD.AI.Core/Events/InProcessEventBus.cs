using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace JD.AI.Core.Events;

/// <summary>
/// In-process event bus backed by <see cref="Channel{T}"/>.
/// Suitable for single-process gateway; replace with a distributed
/// implementation (Redis Pub/Sub, Azure Service Bus, etc.) for multi-node.
/// </summary>
public sealed class InProcessEventBus : IEventBus, IDisposable
{
    private readonly List<Subscription> _subscriptions = [];
    private readonly Lock _lock = new();

    public Task PublishAsync(GatewayEvent evt, CancellationToken ct = default)
    {
        List<Subscription> targets;
        lock (_lock)
        {
            targets = _subscriptions
                .Where(s => s.Filter is null || string.Equals(s.Filter, evt.EventType, StringComparison.Ordinal))
                .ToList();
        }

        return Task.WhenAll(targets.Select(s => s.Dispatch(evt)));
    }

    public IDisposable Subscribe(string? eventTypeFilter, Func<GatewayEvent, Task> handler)
    {
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

        using var sub = Subscribe(eventTypeFilter, async evt =>
        {
            await channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
        });

        await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var sub in _subscriptions)
                sub.MarkRemoved();
            _subscriptions.Clear();
        }
    }

    private void Remove(Subscription sub)
    {
        lock (_lock) _subscriptions.Remove(sub);
    }

    private sealed class Subscription(
        string? filter,
        Func<GatewayEvent, Task> handler,
        InProcessEventBus bus) : IDisposable
    {
        private bool _removed;
        public string? Filter => filter;

        public Task Dispatch(GatewayEvent evt) =>
            _removed ? Task.CompletedTask : handler(evt);

        public void MarkRemoved() => _removed = true;

        public void Dispose()
        {
            _removed = true;
            bus.Remove(this);
        }
    }
}
