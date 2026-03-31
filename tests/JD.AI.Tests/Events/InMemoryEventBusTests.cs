using System.Threading.Channels;
using FluentAssertions;
using JD.AI.Core.Events;

namespace JD.AI.Tests.Events;

/// <summary>
/// Tests for <see cref="InMemoryEventBus"/> publish, subscribe, stream,
/// GetEvents, and dispose behaviour.
/// </summary>
public sealed class InMemoryEventBusTests
{
    private static GatewayEvent SampleEvent(
        string eventType = "test.event",
        string? sourceId = "src-1") =>
        new GatewayEvent(eventType, sourceId ?? "src-1", DateTimeOffset.UtcNow);

    // ── Publish / GetEvents ──────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_StoresEventInBuffer()
    {
        var bus = InMemoryEventBus.Create();
        await bus.PublishAsync(SampleEvent());
        await bus.PublishAsync(SampleEvent("other.event"));

        var events = await bus.GetEvents();

        events.Should().HaveCount(2);
        events.Select(e => e.EventType).Should().Contain("test.event");
        events.Select(e => e.EventType).Should().Contain("other.event");
    }

    [Fact]
    public async Task GetEvents_ReturnsNewestLast()
    {
        var bus = InMemoryEventBus.Create();
        await bus.PublishAsync(SampleEvent("first"));
        await bus.PublishAsync(SampleEvent("second"));

        var events = await bus.GetEvents();

        events[0].EventType.Should().Be("first");
        events[1].EventType.Should().Be("second");
    }

    [Fact]
    public async Task GetEvents_AfterCancellation_ReturnsWhatWasPublished()
    {
        var bus = InMemoryEventBus.Create();
        await bus.PublishAsync(SampleEvent("a"));
        await bus.PublishAsync(SampleEvent("b"));

        var cts = new CancellationTokenSource();
        var events = await bus.GetEvents(cts.Token);

        events.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEvents_Cancellation_ThrowsOperationCancelled()
    {
        var bus = InMemoryEventBus.Create();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await bus.GetEvents(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Subscribe ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_WithFilter_OnlyReceivesMatchingEvents()
    {
        var bus = InMemoryEventBus.Create();
        var received = new List<GatewayEvent>();

        using var subscription = bus.Subscribe("matched.event", evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new GatewayEvent("other", "src", DateTimeOffset.UtcNow));
        await bus.PublishAsync(new GatewayEvent("matched.event", "src", DateTimeOffset.UtcNow));
        await bus.PublishAsync(new GatewayEvent("matched.event", "src", DateTimeOffset.UtcNow));

        await Task.Delay(50); // allow async dispatch

        received.Should().HaveCount(2);
        received.All(e => e.EventType == "matched.event").Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_NullFilter_ReceivesAllEvents()
    {
        var bus = InMemoryEventBus.Create();
        var received = new List<GatewayEvent>();

        using var subscription = bus.Subscribe(null, evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(SampleEvent("a"));
        await bus.PublishAsync(SampleEvent("b"));

        await Task.Delay(50);

        received.Should().HaveCount(2);
    }

    [Fact]
    public async Task Subscribe_ReturnsDisposable()
    {
        var bus = InMemoryEventBus.Create();
        var received = new List<GatewayEvent>();

        var subscription = bus.Subscribe("x", evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(SampleEvent("x"));
        subscription.Dispose();
        await bus.PublishAsync(SampleEvent("x"));

        await Task.Delay(50);

        received.Should().HaveCount(1);
    }

    // ── StreamAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_YieldsMatchingEvents()
    {
        var bus = InMemoryEventBus.Create();

        var publishTask = Task.Run(async () =>
        {
            await bus.PublishAsync(SampleEvent("stream.event.1"));
            await Task.Delay(10);
            await bus.PublishAsync(SampleEvent("stream.event.2"));
            await Task.Delay(10);
            await bus.PublishAsync(SampleEvent("other"));
        });

        var received = new List<GatewayEvent>();
        await foreach (var evt in bus.StreamAsync("stream.event", CancellationToken.None))
        {
            received.Add(evt);
            if (received.Count == 2) break;
        }

        received.Should().HaveCount(2);
        received.All(e => e.EventType.StartsWith("stream.event")).Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_NullFilter_YieldsAllEvents()
    {
        var bus = InMemoryEventBus.Create();

        var publishTask = Task.Run(async () =>
        {
            await bus.PublishAsync(SampleEvent("all.1"));
            await bus.PublishAsync(SampleEvent("all.2"));
        });

        var received = new List<GatewayEvent>();
        await foreach (var evt in bus.StreamAsync(null, CancellationToken.None))
        {
            received.Add(evt);
            if (received.Count == 2) break;
        }

        received.Should().HaveCount(2);
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_CompletesChannel()
    {
        IEventBus bus = InMemoryEventBus.Create();
        ((IDisposable)bus).Dispose();

        var act = async () => await bus.PublishAsync(SampleEvent());

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PublishAsync_AfterDispose_ThrowsObjectDisposed()
    {
        IEventBus bus = InMemoryEventBus.Create();
        ((IDisposable)bus).Dispose();

        var act = async () => await bus.PublishAsync(
            new GatewayEvent("x", "s", DateTimeOffset.UtcNow));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_ImplementsIdisposable()
    {
        var bus = InMemoryEventBus.Create() as IDisposable;
        bus.Should().NotBeNull();
    }

    // ── Concurrent publish ──────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_MultipleWriters_AllStored()
    {
        var bus = InMemoryEventBus.Create();

        await Task.WhenAll(
            Enumerable.Range(0, 10).Select(i =>
                bus.PublishAsync(new GatewayEvent($"event.{i}", "src", DateTimeOffset.UtcNow))));

        var events = await bus.GetEvents();

        events.Should().HaveCount(10);
    }
}