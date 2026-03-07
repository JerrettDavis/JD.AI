using FluentAssertions;
using JD.AI.Core.Events;

namespace JD.AI.Tests.Events;

public sealed class InProcessEventBusTests
{
    private static GatewayEvent Evt(string type = "test", string source = "src") =>
        new(type, source, DateTimeOffset.UtcNow);

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNotThrow()
    {
        using var bus = new InProcessEventBus();
        await bus.PublishAsync(Evt());
    }

    [Fact]
    public async Task Subscribe_ReceivesPublishedEvent()
    {
        using var bus = new InProcessEventBus();
        GatewayEvent? received = null;

        bus.Subscribe(null, evt =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        var published = Evt();
        await bus.PublishAsync(published);

        received.Should().NotBeNull();
        received!.EventType.Should().Be("test");
    }

    [Fact]
    public async Task Subscribe_WithFilter_OnlyReceivesMatchingEvents()
    {
        using var bus = new InProcessEventBus();
        var received = new List<GatewayEvent>();

        bus.Subscribe("agent.started", evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(Evt("agent.started"));
        await bus.PublishAsync(Evt("agent.completed"));
        await bus.PublishAsync(Evt("agent.started"));

        received.Should().HaveCount(2);
        received.Should().AllSatisfy(
            e => e.EventType.Should().Be("agent.started"));
    }

    [Fact]
    public async Task Subscribe_NullFilter_ReceivesAllEvents()
    {
        using var bus = new InProcessEventBus();
        var received = new List<GatewayEvent>();

        bus.Subscribe(null, evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(Evt("type-a"));
        await bus.PublishAsync(Evt("type-b"));

        received.Should().HaveCount(2);
    }

    [Fact]
    public async Task Subscribe_MultipleSubscribers_AllReceive()
    {
        using var bus = new InProcessEventBus();
        var count1 = 0;
        var count2 = 0;

        bus.Subscribe(null, _ => { count1++; return Task.CompletedTask; });
        bus.Subscribe(null, _ => { count2++; return Task.CompletedTask; });

        await bus.PublishAsync(Evt());

        count1.Should().Be(1);
        count2.Should().Be(1);
    }

    [Fact]
    public async Task Subscribe_Dispose_StopsReceiving()
    {
        using var bus = new InProcessEventBus();
        var count = 0;

        var sub = bus.Subscribe(null, _ => { count++; return Task.CompletedTask; });
        await bus.PublishAsync(Evt());
        count.Should().Be(1);

        sub.Dispose();
        await bus.PublishAsync(Evt());
        count.Should().Be(1); // no increment after dispose
    }

    [Fact]
    public async Task Dispose_ClearsAllSubscriptions()
    {
        var bus = new InProcessEventBus();
        var count = 0;

        bus.Subscribe(null, _ => { count++; return Task.CompletedTask; });
        bus.Dispose();

        await bus.PublishAsync(Evt());
        count.Should().Be(0);
    }

    [Fact]
    public async Task StreamAsync_ReceivesEvents()
    {
        using var bus = new InProcessEventBus();
        using var cts = new CancellationTokenSource();

        var received = new List<GatewayEvent>();

        var streamTask = Task.Run(async () =>
        {
            await foreach (var evt in bus.StreamAsync(null, cts.Token))
            {
                received.Add(evt);
                if (received.Count >= 2)
                    await cts.CancelAsync();
            }
        });

        // Give stream time to subscribe
        await Task.Delay(50);

        await bus.PublishAsync(Evt("e1"));
        await bus.PublishAsync(Evt("e2"));

        try { await streamTask; }
        catch (OperationCanceledException) { /* expected */ }

        received.Should().HaveCount(2);
    }

    [Fact]
    public async Task StreamAsync_WithFilter_OnlyMatchingEvents()
    {
        using var bus = new InProcessEventBus();
        using var cts = new CancellationTokenSource();

        var received = new List<GatewayEvent>();

        var streamTask = Task.Run(async () =>
        {
            await foreach (var evt in bus.StreamAsync("target", cts.Token))
            {
                received.Add(evt);
                if (received.Count >= 1)
                    await cts.CancelAsync();
            }
        });

        await Task.Delay(50);

        await bus.PublishAsync(Evt("other"));
        await bus.PublishAsync(Evt("target"));

        try { await streamTask; }
        catch (OperationCanceledException) { /* expected */ }

        received.Should().ContainSingle();
        received[0].EventType.Should().Be("target");
    }

    [Fact]
    public void GatewayEvent_HasUniqueId()
    {
        var e1 = Evt();
        var e2 = Evt();
        e1.Id.Should().NotBeNullOrEmpty();
        e2.Id.Should().NotBeNullOrEmpty();
        e1.Id.Should().NotBe(e2.Id);
    }

    [Fact]
    public void GatewayEvent_StoresPayload()
    {
        var payload = new { Key = "val" };
        var evt = new GatewayEvent("test", "src", DateTimeOffset.UtcNow, payload);
        evt.Payload.Should().Be(payload);
    }
}
