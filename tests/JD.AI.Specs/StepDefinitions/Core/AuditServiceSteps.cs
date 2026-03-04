using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using Reqnroll;

namespace JD.AI.Specs.StepDefinitions.Core;

[Binding]
public sealed class AuditServiceSteps
{
    private readonly ScenarioContext _context;

    public AuditServiceSteps(ScenarioContext context) => _context = context;

    [Given(@"an audit service with (\d+) mock sink(?:s)?")]
    public void GivenAnAuditServiceWithMockSinks(int count)
    {
        var sinks = Enumerable.Range(0, count).Select(_ => new MockAuditSink()).ToList();
        _context.Set(sinks, "MockSinks");
        _context.Set(new AuditService(sinks), "AuditService");
    }

    [Given(@"an audit service with (\d+) failing sink and (\d+) working sink")]
    public void GivenAnAuditServiceWithFailingAndWorkingSinks(int failCount, int workCount)
    {
        var sinks = new List<IAuditSink>();
        for (var i = 0; i < failCount; i++)
            sinks.Add(new FailingAuditSink());
        var workingSinks = new List<MockAuditSink>();
        for (var i = 0; i < workCount; i++)
        {
            var mock = new MockAuditSink();
            sinks.Add(mock);
            workingSinks.Add(mock);
        }
        _context.Set(workingSinks, "WorkingSinks");
        _context.Set(new AuditService(sinks), "AuditService");
    }

    [When(@"I emit an audit event with action ""([^""]+)""")]
    public async Task WhenIEmitAnAuditEventWithAction(string action)
    {
        var service = _context.Get<AuditService>("AuditService");
        var evt = new AuditEvent { Action = action };
        await service.EmitAsync(evt);
        _context.Set(evt, "LastEvent");
    }

    [When(@"I emit an audit event with action ""([^""]+)"" and resource ""([^""]+)""")]
    public async Task WhenIEmitAnAuditEventWithActionAndResource(string action, string resource)
    {
        var service = _context.Get<AuditService>("AuditService");
        var evt = new AuditEvent { Action = action, Resource = resource };
        await service.EmitAsync(evt);
        _context.Set(evt, "LastEvent");
    }

    [When(@"I emit an audit event with action ""([^""]+)"" and severity ""([^""]+)""")]
    public async Task WhenIEmitAnAuditEventWithActionAndSeverity(string action, string severity)
    {
        var service = _context.Get<AuditService>("AuditService");
        var sev = Enum.Parse<AuditSeverity>(severity);
        var evt = new AuditEvent { Action = action, Severity = sev };
        await service.EmitAsync(evt);
        _context.Set(evt, "LastEvent");
    }

    [When(@"I flush the audit service")]
    public async Task WhenIFlushTheAuditService()
    {
        var service = _context.Get<AuditService>("AuditService");
        await service.FlushAsync();
    }

    [Then(@"the mock sink should have received (\d+) event")]
    public void ThenTheMockSinkShouldHaveReceivedEvents(int count)
    {
        var sinks = _context.Get<List<MockAuditSink>>("MockSinks");
        sinks[0].Events.Should().HaveCount(count);
    }

    [Then(@"all (\d+) sinks should have received (\d+) event each")]
    public void ThenAllSinksShouldHaveReceivedEventEach(int sinkCount, int eventCount)
    {
        var sinks = _context.Get<List<MockAuditSink>>("MockSinks");
        sinks.Should().HaveCount(sinkCount);
        foreach (var sink in sinks)
            sink.Events.Should().HaveCount(eventCount);
    }

    [Then(@"the working sink should have received (\d+) event")]
    public void ThenTheWorkingSinkShouldHaveReceivedEvent(int count)
    {
        var sinks = _context.Get<List<MockAuditSink>>("WorkingSinks");
        sinks[0].Events.Should().HaveCount(count);
    }

    [Then(@"the received event action should be ""(.*)""")]
    public void ThenTheReceivedEventActionShouldBe(string expected)
    {
        var sinks = _context.Get<List<MockAuditSink>>("MockSinks");
        sinks[0].Events[0].Action.Should().Be(expected);
    }

    [Then(@"all sinks should have been flushed")]
    public void ThenAllSinksShouldHaveBeenFlushed()
    {
        var sinks = _context.Get<List<MockAuditSink>>("MockSinks");
        foreach (var sink in sinks)
            sink.FlushCount.Should().BeGreaterThan(0);
    }

    [Then(@"the received event should have severity ""(.*)""")]
    public void ThenTheReceivedEventShouldHaveSeverity(string expected)
    {
        var sinks = _context.Get<List<MockAuditSink>>("MockSinks");
        sinks[0].Events[0].Severity.ToString().Should().Be(expected);
    }

    [Then(@"the received event should have a non-empty event ID")]
    public void ThenTheReceivedEventShouldHaveNonEmptyEventId()
    {
        var sinks = _context.Get<List<MockAuditSink>>("MockSinks");
        sinks[0].Events[0].EventId.Should().NotBeNullOrEmpty();
    }

    private sealed class MockAuditSink : IAuditSink
    {
        public string Name => "mock";
        public List<AuditEvent> Events { get; } = [];
        public int FlushCount { get; private set; }

        public Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }

        public Task FlushAsync(CancellationToken ct = default)
        {
            FlushCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingAuditSink : IAuditSink
    {
        public string Name => "failing";

        public Task WriteAsync(AuditEvent evt, CancellationToken ct = default)
            => throw new InvalidOperationException("Sink failure");

        public Task FlushAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("Flush failure");
    }
}
