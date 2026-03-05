using FluentAssertions;
using JD.AI.Core.Governance;
using JD.AI.Core.Governance.Audit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Governance.Audit;

public sealed class AuditServiceTests
{
    private static AuditEvent MakeEvent() => new()
    {
        Action = "test-action",
        Severity = AuditSeverity.Info,
    };

    [Fact]
    public async Task EmitAsync_DispatchesToAllSinks()
    {
        var sink1 = Substitute.For<IAuditSink>();
        var sink2 = Substitute.For<IAuditSink>();
        var service = new AuditService([sink1, sink2]);
        var evt = MakeEvent();

        await service.EmitAsync(evt);

        await sink1.Received(1).WriteAsync(evt, default);
        await sink2.Received(1).WriteAsync(evt, default);
    }

    [Fact]
    public async Task EmitAsync_SinkThrows_DoesNotPropagate()
    {
        var failingSink = Substitute.For<IAuditSink>();
        failingSink.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sink failure"));

        var service = new AuditService([failingSink]);
        var evt = MakeEvent();

        var act = async () => await service.EmitAsync(evt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EmitAsync_EmptySinks_WorksWithoutErrors()
    {
        var service = new AuditService([]);
        var evt = MakeEvent();

        var act = async () => await service.EmitAsync(evt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EmitAsync_FirstSinkFails_SecondSinkStillReceivesEvent()
    {
        var failingSink = Substitute.For<IAuditSink>();
        failingSink.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("fail"));

        var workingSink = Substitute.For<IAuditSink>();
        var service = new AuditService([failingSink, workingSink]);
        var evt = MakeEvent();

        await service.EmitAsync(evt);

        await workingSink.Received(1).WriteAsync(evt, default);
    }

    [Fact]
    public async Task FlushAsync_CallsFlushOnAllSinks()
    {
        var sink1 = Substitute.For<IAuditSink>();
        var sink2 = Substitute.For<IAuditSink>();
        var service = new AuditService([sink1, sink2]);

        await service.FlushAsync();

        await sink1.Received(1).FlushAsync(default);
        await sink2.Received(1).FlushAsync(default);
    }

    [Fact]
    public async Task FlushAsync_SinkFlushThrows_DoesNotPropagate()
    {
        var failingSink = Substitute.For<IAuditSink>();
        failingSink.FlushAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("flush failed"));

        var service = new AuditService([failingSink]);

        var act = async () => await service.FlushAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EmitAsync_EventWithPolicyResult_PassedToSink()
    {
        var sink = Substitute.For<IAuditSink>();
        var service = new AuditService([sink]);

        var evt = new AuditEvent
        {
            Action = "tool.invoke",
            PolicyResult = PolicyDecision.Deny,
            Severity = AuditSeverity.Warning,
        };

        await service.EmitAsync(evt);

        await sink.Received(1).WriteAsync(Arg.Is<AuditEvent>(e =>
            e.PolicyResult == PolicyDecision.Deny &&
            e.Severity == AuditSeverity.Warning), default);
    }

    [Fact]
    public async Task EmitAsync_SinkFailure_EnqueuesDeadLetter()
    {
        var failingSink = Substitute.For<IAuditSink>();
        failingSink.Name.Returns("failing-sink");
        failingSink.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sink failure"));

        var service = new AuditService([failingSink]);
        var evt = MakeEvent();

        await service.EmitAsync(evt);

        service.DeadLetterCount.Should().Be(1);
        var deadLetters = service.DrainDeadLetters();
        deadLetters.Should().HaveCount(1);
        deadLetters[0].SinkName.Should().Be("failing-sink");
        deadLetters[0].Event.Should().BeSameAs(evt);
        deadLetters[0].ErrorMessage.Should().Be("Sink failure");
    }

    [Fact]
    public async Task EmitAsync_MultipleSinkFailures_TracksAll()
    {
        var sink1 = Substitute.For<IAuditSink>();
        sink1.Name.Returns("s1");
        sink1.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("s1 fail"));

        var sink2 = Substitute.For<IAuditSink>();
        sink2.Name.Returns("s2");
        sink2.WriteAsync(Arg.Any<AuditEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("s2 fail"));

        var service = new AuditService([sink1, sink2]);
        await service.EmitAsync(MakeEvent());

        service.DeadLetterCount.Should().Be(2);
    }

    [Fact]
    public void DrainDeadLetters_EmptiesQueue()
    {
        var service = new AuditService([]);
        var deadLetters = service.DrainDeadLetters();
        deadLetters.Should().BeEmpty();
    }

    [Fact]
    public void AuditEvent_ToolFields_AreSettable()
    {
        var evt = new AuditEvent
        {
            Action = "tool.invoke",
            ToolName = "web_search",
            ToolArguments = "query=test",
            ToolResult = "Found 5 results",
            DurationMs = 1234,
        };

        evt.ToolName.Should().Be("web_search");
        evt.ToolArguments.Should().Be("query=test");
        evt.ToolResult.Should().Be("Found 5 results");
        evt.DurationMs.Should().Be(1234);
    }

    [Fact]
    public void AuditEvent_PreviousHash_ForTamperEvidence()
    {
        var evt = new AuditEvent
        {
            Action = "test",
            PreviousHash = "abc123def456",
        };

        evt.PreviousHash.Should().Be("abc123def456");
    }
}
