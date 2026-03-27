using JD.AI.Core.Events;

namespace JD.AI.Gateway.Tests;

public sealed class EventBusTests
{
    [Fact]
    public async Task Publish_NotifiesSubscribers()
    {
        using var bus = new InProcessEventBus();
        GatewayEvent? received = null;
        bus.Subscribe(null, evt => { received = evt; return Task.CompletedTask; });

        await bus.PublishAsync(new GatewayEvent("test", "src", DateTimeOffset.UtcNow));

        Assert.NotNull(received);
        Assert.Equal("test", received!.EventType);
    }

    [Fact]
    public async Task Subscribe_WithFilter_OnlyReceivesMatching()
    {
        using var bus = new InProcessEventBus();
        var count = 0;
        bus.Subscribe("target", _ => { count++; return Task.CompletedTask; });

        await bus.PublishAsync(new GatewayEvent("other", "src", DateTimeOffset.UtcNow));
        await bus.PublishAsync(new GatewayEvent("target", "src", DateTimeOffset.UtcNow));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Unsubscribe_StopsNotifications()
    {
        using var bus = new InProcessEventBus();
        var count = 0;
        var sub = bus.Subscribe(null, _ => { count++; return Task.CompletedTask; });

        await bus.PublishAsync(new GatewayEvent("a", "src", DateTimeOffset.UtcNow));
        sub.Dispose();
        await bus.PublishAsync(new GatewayEvent("b", "src", DateTimeOffset.UtcNow));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StreamAsync_YieldsEvents()
    {
        using var bus = new InProcessEventBus();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var events = new List<GatewayEvent>();
        var streamTask = Task.Run(async () =>
        {
            await foreach (var evt in bus.StreamAsync(null, cts.Token))
            {
                events.Add(evt);
                if (events.Count >= 2) break;
            }
        }, cts.Token);

        // Give the stream time to subscribe (generous for slow CI runners)
        await Task.Delay(500);

        await bus.PublishAsync(new GatewayEvent("e1", "src", DateTimeOffset.UtcNow));
        await bus.PublishAsync(new GatewayEvent("e2", "src", DateTimeOffset.UtcNow));

        await streamTask;
        Assert.Equal(2, events.Count);
    }
}
