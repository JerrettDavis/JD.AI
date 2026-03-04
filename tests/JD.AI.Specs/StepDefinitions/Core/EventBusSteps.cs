using FluentAssertions;
using JD.AI.Core.Events;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class EventBusSteps : IDisposable
{
    private readonly ScenarioContext _context;
    private InProcessEventBus? _bus;

    public EventBusSteps(ScenarioContext context) => _context = context;

    [Given(@"an in-process event bus")]
    public void GivenAnInProcessEventBus()
    {
        _bus = new InProcessEventBus();
        _context.Set(_bus, "EventBus");
    }

    [Given(@"a subscriber for event type ""(.*)""")]
    public void GivenASubscriberForEventType(string eventType)
    {
        var bus = _context.Get<InProcessEventBus>("EventBus");
        var received = new List<GatewayEvent>();
        var sub = bus.Subscribe(eventType, evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });
        _context.Set(received, "ReceivedEvents");
        _context.Set(sub, "Subscription");
    }

    [Given(@"(\d+) subscribers for event type ""(.*)""")]
    public void GivenMultipleSubscribersForEventType(int count, string eventType)
    {
        var bus = _context.Get<InProcessEventBus>("EventBus");
        var allReceived = new List<List<GatewayEvent>>();
        var subs = new List<IDisposable>();
        for (var i = 0; i < count; i++)
        {
            var received = new List<GatewayEvent>();
            allReceived.Add(received);
            subs.Add(bus.Subscribe(eventType, evt =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }));
        }
        _context.Set(allReceived, "AllReceivedEvents");
        _context.Set(subs, "Subscriptions");
    }

    [Given(@"a subscriber with no event type filter")]
    public void GivenASubscriberWithNoFilter()
    {
        var bus = _context.Get<InProcessEventBus>("EventBus");
        var received = new List<GatewayEvent>();
        var sub = bus.Subscribe(null, evt =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });
        _context.Set(received, "ReceivedEvents");
        _context.Set(sub, "Subscription");
    }

    [When(@"I publish an event of type ""(.*)"" from source ""(.*)""")]
    public async Task WhenIPublishAnEvent(string eventType, string source)
    {
        var bus = _context.Get<InProcessEventBus>("EventBus");
        var evt = new GatewayEvent(eventType, source, DateTimeOffset.UtcNow);
        await bus.PublishAsync(evt);
    }

    [When(@"the subscriber is disposed")]
    public void WhenTheSubscriberIsDisposed()
    {
        var sub = _context.Get<IDisposable>("Subscription");
        sub.Dispose();
    }

    [Then(@"the subscriber should have received (\d+) events?")]
    public void ThenTheSubscriberShouldHaveReceivedEvents(int count)
    {
        var received = _context.Get<List<GatewayEvent>>("ReceivedEvents");
        received.Should().HaveCount(count);
    }

    [Then(@"the received event type should be ""(.*)""")]
    public void ThenTheReceivedEventTypeShouldBe(string expected)
    {
        var received = _context.Get<List<GatewayEvent>>("ReceivedEvents");
        received[0].EventType.Should().Be(expected);
    }

    [Then(@"all (\d+) subscribers should have received (\d+) event")]
    public void ThenAllSubscribersShouldHaveReceivedEvent(int subscriberCount, int eventCount)
    {
        var allReceived = _context.Get<List<List<GatewayEvent>>>("AllReceivedEvents");
        allReceived.Should().HaveCount(subscriberCount);
        foreach (var received in allReceived)
            received.Should().HaveCount(eventCount);
    }

    public void Dispose()
    {
        _bus?.Dispose();
    }
}
