using System.Threading.Channels;

namespace JD.AI.Core.Events;

/// <summary>
/// In-memory ring-buffer event bus that also serves as the audit log.
/// All events published are retained up to <paramref name="capacity"/> and
/// can be retrieved via <see cref="GetEvents()"/> for the Dashboard Logs page.
/// </summary>
public sealed class InMemoryEventBus : IEventBus, IDisposable
{
    private readonly Channel<GatewayEvent> _channel;
    private readonly List<Subscription> _subscriptions = [];
    private readonly Lock _lock = new();
    private bool _disposed;

    public InMemoryEventBus(int capacity = 10_000)
    {
        _channel = Channel.CreateBounded<GatewayEvent>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });
    }

    /// <summary>
    /// Creates a new <see cref="IEventBus"/> backed by an in-memory ring buffer.
    /// Use as the factory in DI: <c>services.AddSingleton&lt;IEventBus&gt;(InMemoryEventBus.Create());</c>
    /// </summary>
    public static IEventBus Create(int capacity = 10_000) => new InMemoryEventBus(capacity);

    /// <summary>
    /// Returns a snapshot of all events currently in the ring buffer, oldest first.
    /// Intended for audit / history queries from the Dashboard Logs page.
    /// </summary>
    public async Task<GatewayEvent[]> GetEvents(CancellationToken ct = default)
    {
        var list = new List<GatewayEvent>();
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                list.Add(evt);
        }
        catch (OperationCanceledException) { /* return what we have */ }
        catch (Exception) { /* reader closed — return what we have */ }
        return list.ToArray();
    }

    public Task PublishAsync(GatewayEvent evt, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            // Dispatch to all live subscriptions.
            foreach (var sub in _subscriptions.ToArray())
                _ = sub.DispatchAsync(evt);
        }

        // Always enqueue so GetEvents() captures the event.
        _channel.Writer.TryWrite(evt);
        return Task.CompletedTask;
    }

    public IDisposable Subscribe(string? eventTypeFilter, Func<GatewayEvent, Task> handler)
    {
        ThrowIfDisposed();
        var sub = new Subscription(eventTypeFilter, handler);
        lock (_lock) _subscriptions.Add(sub);
        return sub;
    }

    public async IAsyncEnumerable<GatewayEvent> StreamAsync(
        string? eventTypeFilter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (eventTypeFilter is null ||
                string.Equals(evt.EventType, eventTypeFilter, StringComparison.Ordinal))
                yield return evt;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        _disposed = true;
        _channel.Writer.Complete();
        lock (_lock) _subscriptions.Clear();
    }

    private sealed class Subscription : IDisposable
    {
        public Subscription(string? filter, Func<GatewayEvent, Task> handler)
        {
            Filter = filter;
            Handler = handler;
        }

        private bool _removed;
        public string? Filter { get; }
        private Func<GatewayEvent, Task> Handler { get; }

        public Task DispatchAsync(GatewayEvent evt) =>
            _removed ? Task.CompletedTask : Handler(evt);

        public void Dispose() => _removed = true;
    }
}