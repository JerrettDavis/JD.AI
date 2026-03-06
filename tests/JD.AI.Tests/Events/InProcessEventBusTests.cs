using JD.AI.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace JD.AI.Tests.Events;

public sealed class InProcessEventBusTests
{
    private static GatewayEvent NewEvent(string type, string source = "test") =>
        new(type, source, DateTimeOffset.UtcNow);

    // ── PublishAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_NoSubscribers_CompletesWithoutError()
    {
        using var bus = new InProcessEventBus();
        await bus.PublishAsync(NewEvent("test.event"));
    }

    [Fact]
    public async Task PublishAsync_DeliversToMatchingSubscriber()
    {
        using var bus = new InProcessEventBus();
        GatewayEvent? received = null;

        using var _ = bus.Subscribe("test.event", evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var expected = NewEvent("test.event");
        await bus.PublishAsync(expected);

        Assert.NotNull(received);
        Assert.Equal(expected.Id, received.Id);
    }

    [Fact]
    public async Task PublishAsync_DoesNotDeliverToNonMatchingSubscriber()
    {
        using var bus = new InProcessEventBus();
        var count = 0;

        using var _ = bus.Subscribe("other.event", _ =>
        {
            count++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(NewEvent("test.event"));

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task PublishAsync_DeliversToCatchAllSubscriber()
    {
        using var bus = new InProcessEventBus();
        var count = 0;

        using var _ = bus.Subscribe(null, _ =>
        {
            count++;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(NewEvent("event.a"));
        await bus.PublishAsync(NewEvent("event.b"));

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task PublishAsync_DeliversToMultipleMatchingSubscribers()
    {
        using var bus = new InProcessEventBus();
        var count = 0;

        using var s1 = bus.Subscribe("same", _ => { count++; return Task.CompletedTask; });
        using var s2 = bus.Subscribe("same", _ => { count++; return Task.CompletedTask; });

        await bus.PublishAsync(NewEvent("same"));

        Assert.Equal(2, count);
    }

    // ── Subscribe / unsubscribe ─────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_DisposingSubscription_StopsDelivery()
    {
        using var bus = new InProcessEventBus();
        var count = 0;

        var sub = bus.Subscribe("target", _ => { count++; return Task.CompletedTask; });
        await bus.PublishAsync(NewEvent("target"));
        sub.Dispose();
        await bus.PublishAsync(NewEvent("target"));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Dispose_BusDisposed_StopsDeliveryToAllSubscribers()
    {
        var bus = new InProcessEventBus();
        var count = 0;

        bus.Subscribe("any", _ => { count++; return Task.CompletedTask; });
        await bus.PublishAsync(NewEvent("any"));

        bus.Dispose();
        await bus.PublishAsync(NewEvent("any")); // subscriptions marked removed

        Assert.Equal(1, count);
    }

    [Fact]
    public void Subscribe_ReturnsDisposableHandle()
    {
        using var bus = new InProcessEventBus();
        var handle = bus.Subscribe("x", _ => Task.CompletedTask);
        Assert.NotNull(handle);
        handle.Dispose(); // should not throw
    }

    // ── StreamAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_ReceivesPublishedEvents()
    {
        using var bus = new InProcessEventBus();
        using var cts = new CancellationTokenSource();

        var received = new List<GatewayEvent>();
        var streamTask = Task.Run(async () =>
        {
            await foreach (var evt in bus.StreamAsync("streamed", cts.Token))
                received.Add(evt);
        });

        // Give the stream a moment to set up its subscription
        await Task.Delay(50);

        await bus.PublishAsync(NewEvent("streamed"));
        await bus.PublishAsync(NewEvent("streamed"));

        await Task.Delay(50);
        await cts.CancelAsync();

        try { await streamTask; }
        catch (OperationCanceledException) { /* expected */ }

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public async Task StreamAsync_FilterEventType_IgnoresOtherTypes()
    {
        using var bus = new InProcessEventBus();
        using var cts = new CancellationTokenSource();

        var received = new List<string>();
        var streamTask = Task.Run(async () =>
        {
            await foreach (var evt in bus.StreamAsync("wanted", cts.Token))
                received.Add(evt.EventType);
        });

        await Task.Delay(50);

        await bus.PublishAsync(NewEvent("wanted"));
        await bus.PublishAsync(NewEvent("ignored"));

        await Task.Delay(50);
        await cts.CancelAsync();

        try { await streamTask; }
        catch (OperationCanceledException) { /* expected */ }

        Assert.All(received, t => Assert.Equal("wanted", t));
        Assert.Single(received);
    }

    // ── GatewayEvent model ──────────────────────────────────────────────────────

    [Fact]
    public void GatewayEvent_HasUniqueId()
    {
        var e1 = new GatewayEvent("t", "s", DateTimeOffset.UtcNow);
        var e2 = new GatewayEvent("t", "s", DateTimeOffset.UtcNow);
        Assert.NotEqual(e1.Id, e2.Id);
    }

    [Fact]
    public void GatewayEvent_PayloadOptional()
    {
        var evt = new GatewayEvent("t", "s", DateTimeOffset.UtcNow);
        Assert.Null(evt.Payload);
    }

    [Fact]
    public void GatewayEvent_PayloadRoundTrips()
    {
        var payload = new { Value = 42 };
        var evt = new GatewayEvent("t", "s", DateTimeOffset.UtcNow, payload);
        Assert.Same(payload, evt.Payload);
    }

    // ── EventBusServiceExtensions ───────────────────────────────────────────────

    [Fact]
    public void AddEventBus_Default_RegistersInProcess()
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        using var sp = services.BuildServiceProvider();

        var bus = sp.GetRequiredService<IEventBus>();
        Assert.IsType<InProcessEventBus>(bus);
    }

    [Fact]
    public void AddEventBus_NullOptions_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddEventBus(null);
        using var sp = services.BuildServiceProvider();

        var bus = sp.GetRequiredService<IEventBus>();
        Assert.IsType<InProcessEventBus>(bus);
    }

    [Fact]
    public void AddEventBus_RedisProvider_WithoutConnectionString_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddEventBus(new EventBusOptions { Provider = "Redis" }));
    }
}
