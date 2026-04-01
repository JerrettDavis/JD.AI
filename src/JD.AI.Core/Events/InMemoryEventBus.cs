using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace JD.AI.Core.Events;

/// <summary>
/// In-memory ring-buffer event bus that also serves as the audit log.
/// All events published are retained up to <paramref name="capacity"/> entries and
/// can be retrieved via <see cref="GetEvents()"/> for the Dashboard Logs page.
/// </summary>
/// <remarks>
/// Design:
/// <list type="bullet">
///   <item><description><see cref="GetEvents"/> returns a non-blocking snapshot of the history ring buffer.</description></item>
///   <item><description><see cref="PublishAsync"/> applies subscription filters before dispatching.</description></item>
///   <item><description><see cref="StreamAsync"/> streams live events from an unbounded channel; filter uses prefix matching.</description></item>
/// </list>
/// </remarks>
public sealed class InMemoryEventBus : IEventBus, IDisposable
{
    // Unbounded channel used only for StreamAsync live streaming.
    private readonly Channel<GatewayEvent> _streamChannel;
    // Bounded ring buffer for GetEvents() history snapshots.
    private readonly Queue<GatewayEvent> _history;
    private readonly int _capacity;
    private readonly List<Subscription> _subscriptions = [];
    private readonly Lock _lock = new();
    private bool _disposed;

    public InMemoryEventBus(int capacity = 10_000)
    {
        _capacity = capacity;
        _history = new Queue<GatewayEvent>(Math.Min(capacity, 4096));
        _streamChannel = Channel.CreateUnbounded<GatewayEvent>(
            new UnboundedChannelOptions { SingleWriter = false, AllowSynchronousContinuations = false });
    }

    /// <summary>
    /// Creates a new <see cref="IEventBus"/> backed by an in-memory ring buffer.
    /// Use as the factory in DI: <c>services.AddSingleton&lt;IEventBus&gt;(InMemoryEventBus.Create());</c>
    /// </summary>
    public static IEventBus Create(int capacity = 10_000) => new InMemoryEventBus(capacity);

    /// <summary>
    /// Returns a non-blocking snapshot of all events currently in the ring buffer, oldest first.
    /// Returns an empty array immediately if <paramref name="ct"/> is already cancelled.
    /// Intended for audit / history queries from the Dashboard Logs page.
    /// </summary>
    public Task<GatewayEvent[]> GetEvents(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (ct.IsCancellationRequested)
            return Task.FromResult(Array.Empty<GatewayEvent>());

        lock (_lock)
            return Task.FromResult(_history.ToArray());
    }

    public Task PublishAsync(GatewayEvent evt, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(evt);

        Subscription[] targets;
        lock (_lock)
        {
            // Maintain bounded ring buffer for GetEvents() history.
            _history.Enqueue(evt);
            while (_history.Count > _capacity)
                _history.Dequeue();

            // Snapshot matching subscriptions while holding the lock; dispatch outside.
            targets = [.. _subscriptions.Where(s => !s.IsRemoved && MatchesFilter(evt.EventType, s.Filter))];
        }

        // Dispatch to subscribers outside the lock to avoid re-entrancy deadlocks.
        foreach (var sub in targets)
            _ = sub.DispatchAsync(evt);

        // Write to stream channel so active StreamAsync consumers receive the event.
        _streamChannel.Writer.TryWrite(evt);
        return Task.CompletedTask;
    }

    public IDisposable Subscribe(string? eventTypeFilter, Func<GatewayEvent, Task> handler)
    {
        ThrowIfDisposed();
        var sub = new Subscription(eventTypeFilter, handler, this);
        lock (_lock) _subscriptions.Add(sub);
        return sub;
    }

    /// <summary>
    /// Streams live events to the caller. The <paramref name="eventTypeFilter"/> is treated
    /// as a prefix: a filter of <c>"agent"</c> matches <c>"agent.started"</c>, <c>"agent.completed"</c>, etc.
    /// Pass <see langword="null"/> to receive all events.
    /// </summary>
    public async IAsyncEnumerable<GatewayEvent> StreamAsync(
        string? eventTypeFilter,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await foreach (var evt in _streamChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (MatchesFilter(evt.EventType, eventTypeFilter))
                yield return evt;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="eventType"/> starts with
    /// <paramref name="filter"/> (or <paramref name="filter"/> is <see langword="null"/>).
    /// </summary>
    private static bool MatchesFilter(string eventType, string? filter) =>
        filter is null || eventType.StartsWith(filter, StringComparison.Ordinal);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _streamChannel.Writer.Complete();
        lock (_lock)
        {
            _subscriptions.Clear();
            _history.Clear();
        }
    }

    private void Remove(Subscription sub)
    {
        lock (_lock) _subscriptions.Remove(sub);
    }

    private sealed class Subscription(
        string? filter,
        Func<GatewayEvent, Task> handler,
        InMemoryEventBus bus) : IDisposable
    {
        private volatile bool _removed;
        public string? Filter => filter;
        public bool IsRemoved => _removed;

        public Task DispatchAsync(GatewayEvent evt) =>
            _removed ? Task.CompletedTask : handler(evt);

        public void Dispose()
        {
            _removed = true;
            bus.Remove(this);
        }
    }
}
