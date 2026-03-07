using System.Threading.Channels;
using FluentAssertions;
using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Unit tests for InMemoryWorkflowDispatcher: argument validation,
/// channel write behavior, and dispose semantics.
/// </summary>
public sealed class InMemoryDispatcherTests
{
    private static Channel<WorkflowWorkItem> MakeChannel(int capacity = 100) =>
        Channel.CreateBounded<WorkflowWorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

    // ── Constructor validation ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullChannel_Throws()
    {
        Action act = () => _ = new InMemoryWorkflowDispatcher(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("channel");
    }

    // ── DispatchAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_NullItem_Throws()
    {
        var dispatcher = new InMemoryWorkflowDispatcher(MakeChannel());

        Func<Task> act = () => dispatcher.DispatchAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DispatchAsync_ValidItem_WritesToChannel()
    {
        var channel = MakeChannel();
        var dispatcher = new InMemoryWorkflowDispatcher(channel);
        var item = new WorkflowWorkItem { WorkflowName = "test-wf" };

        await dispatcher.DispatchAsync(item);

        channel.Reader.TryRead(out var read).Should().BeTrue();
        read.Should().Be(item);
    }

    [Fact]
    public async Task DispatchAsync_MultipleItems_AllWrittenInOrder()
    {
        var channel = MakeChannel();
        var dispatcher = new InMemoryWorkflowDispatcher(channel);

        var items = Enumerable.Range(1, 5)
            .Select(i => new WorkflowWorkItem { WorkflowName = $"wf-{i}" })
            .ToArray();

        foreach (var item in items)
            await dispatcher.DispatchAsync(item);

        var read = new List<WorkflowWorkItem>();
        while (channel.Reader.TryRead(out var r))
            read.Add(r);

        read.Should().HaveCount(5);
        read.Select(r => r.WorkflowName).Should().Equal(items.Select(i => i.WorkflowName));
    }

    [Fact]
    public async Task DispatchAsync_WithCancellationToken_PassesThrough()
    {
        var channel = MakeChannel();
        var dispatcher = new InMemoryWorkflowDispatcher(channel);
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        using var cts = new CancellationTokenSource();

        // Should not throw when token is not cancelled
        await dispatcher.DispatchAsync(item, cts.Token);

        channel.Reader.TryRead(out _).Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WithCancelledToken_Throws()
    {
        // Create a bounded channel with capacity=0 — any write will block
        var channel = Channel.CreateBounded<WorkflowWorkItem>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });

        // Fill the channel to make write block
        await channel.Writer.WriteAsync(new WorkflowWorkItem { WorkflowName = "blocker" });

        var dispatcher = new InMemoryWorkflowDispatcher(channel);
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => dispatcher.DispatchAsync(item, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── DisposeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_CompletesChannelWriter()
    {
        var channel = MakeChannel();
        var dispatcher = new InMemoryWorkflowDispatcher(channel);

        await dispatcher.DisposeAsync();

        // After disposal, writer is complete — further writes fail
        channel.Writer.TryWrite(new WorkflowWorkItem { WorkflowName = "wf" }).Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes_WithoutException()
    {
        var dispatcher = new InMemoryWorkflowDispatcher(MakeChannel());

        Func<Task> act = async () =>
        {
            await dispatcher.DisposeAsync();
            await dispatcher.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Unit tests for InMemoryWorkflowWorkerService: processing, dead-lettering,
/// transient failures, and exception handling.
/// </summary>
public sealed class InMemoryWorkerServiceTests
{
    private static Channel<WorkflowWorkItem> MakeUnboundedChannel() =>
        Channel.CreateUnbounded<WorkflowWorkItem>();

    private static WorkflowWorkItem MakeItem(string name = "test-wf") =>
        new() { WorkflowName = name };

    // ── Constructor validation ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullChannel_Throws()
    {
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = Substitute.For<IDeadLetterSink>();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        Action act = () => _ = new InMemoryWorkflowWorkerService(null!, worker, dlq, logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("channel");
    }

    [Fact]
    public void Constructor_NullWorker_Throws()
    {
        var channel = MakeUnboundedChannel();
        var dlq = Substitute.For<IDeadLetterSink>();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        Action act = () => _ = new InMemoryWorkflowWorkerService(channel, null!, dlq, logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("worker");
    }

    [Fact]
    public void Constructor_NullDlq_Throws()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        Action act = () => _ = new InMemoryWorkflowWorkerService(channel, worker, null!, logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("dlq");
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = Substitute.For<IDeadLetterSink>();

        Action act = () => _ = new InMemoryWorkflowWorkerService(channel, worker, dlq, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    // ── Processing success ────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_Success_DoesNotDeadLetter()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        worker.ProcessAsync(Arg.Any<WorkflowWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(WorkItemResult.Success);

        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(MakeItem("success-wf"), cts.Token);
        await Task.Delay(200, cts.Token);

        dlq.Items.Should().BeEmpty();

        await service.StopAsync(CancellationToken.None);
    }

    // ── Permanent failure → dead-letter ───────────────────────────────────────

    [Fact]
    public async Task Worker_PermanentFailure_DeadLettersItem()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        worker.ProcessAsync(Arg.Any<WorkflowWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(WorkItemResult.Permanent);

        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        var item = MakeItem("perm-fail-wf");
        await channel.Writer.WriteAsync(item, cts.Token);
        await Task.Delay(300, cts.Token);

        dlq.Items.Should().ContainSingle(d => d.Item.WorkflowName == "perm-fail-wf");
        dlq.Items[0].Reason.Should().Contain("Permanent");

        await service.StopAsync(CancellationToken.None);
    }

    // ── Transient failure → no dead-letter ───────────────────────────────────

    [Fact]
    public async Task Worker_TransientFailure_DoesNotDeadLetter()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        worker.ProcessAsync(Arg.Any<WorkflowWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(WorkItemResult.Transient);

        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(MakeItem("transient-wf"), cts.Token);
        await Task.Delay(200, cts.Token);

        dlq.Items.Should().BeEmpty();

        await service.StopAsync(CancellationToken.None);
    }

    // ── Unhandled exception → dead-letter ─────────────────────────────────────

    [Fact]
    public async Task Worker_ThrowsException_DeadLettersWithException()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        var exception = new InvalidOperationException("boom");
        worker.ProcessAsync(Arg.Any<WorkflowWorkItem>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(MakeItem("exception-wf"), cts.Token);
        await Task.Delay(300, cts.Token);

        dlq.Items.Should().ContainSingle(d => d.Item.WorkflowName == "exception-wf");
        dlq.Items[0].Exception.Should().Be(exception);
        dlq.Items[0].Reason.Should().Contain("Unhandled");

        await service.StopAsync(CancellationToken.None);
    }

    // ── Multiple items ────────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_ProcessesMultipleItems_InOrder()
    {
        var channel = MakeUnboundedChannel();
        var processed = new List<string>();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        worker.ProcessAsync(Arg.Any<WorkflowWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<WorkflowWorkItem>();
                processed.Add(item.WorkflowName);
                return Task.FromResult(WorkItemResult.Success);
            });

        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);

        for (var i = 1; i <= 3; i++)
            await channel.Writer.WriteAsync(MakeItem($"wf-{i}"), cts.Token);

        await Task.Delay(400, cts.Token);

        processed.Should().Equal("wf-1", "wf-2", "wf-3");

        await service.StopAsync(CancellationToken.None);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_Cancellation_StopsGracefully()
    {
        var channel = MakeUnboundedChannel();
        var worker = Substitute.For<IWorkflowWorker>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;

        worker.ProcessAsync(Arg.Any<WorkflowWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(WorkItemResult.Success);

        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StartAsync(cts.Token);
        await service.StopAsync(CancellationToken.None);

        // Should have completed without exception
    }
}
