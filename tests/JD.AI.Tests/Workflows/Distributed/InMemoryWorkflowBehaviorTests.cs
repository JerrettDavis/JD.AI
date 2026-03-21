using System.Threading.Channels;
using FluentAssertions;
using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Behavior-focused tests for the in-memory distributed workflow transport.
/// These exercise orchestration edges: dispatcher disposal, failure recovery,
/// and cancellation while a work item is actively being processed.
/// </summary>
public sealed class InMemoryWorkflowBehaviorTests
{
    [Fact]
    public async Task Dispatcher_DisposeAsync_PreventsFurtherDispatch()
    {
        var dispatcher = new InMemoryWorkflowDispatcher(CreateChannel());

        await dispatcher.DisposeAsync();

        Func<Task> act = () => dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "post-dispose" });

        await act.Should().ThrowAsync<ChannelClosedException>();
    }

    [Fact]
    public async Task WorkerService_ExceptionInOneItem_DoesNotBlockLaterItems()
    {
        var processed = new List<string>();
        var goodItemProcessed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var worker = new DelegatingWorker(item =>
        {
            if (string.Equals(item.WorkflowName, "bad-item", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("boom");
            }

            processed.Add(item.WorkflowName);
            goodItemProcessed.TrySetResult(true);
            return WorkItemResult.Success;
        });

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker);
        await service.StartAsync(cts.Token);

        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "bad-item" }, cts.Token);
        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "good-item" }, cts.Token);

        await goodItemProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        dlq.Items.Should().ContainSingle(d => d.Item.WorkflowName == "bad-item");
        dlq.Items[0].Reason.Should().Contain("Unhandled");
        dlq.Items[0].Exception.Should().BeOfType<InvalidOperationException>();
        processed.Should().Equal("good-item");

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WorkerService_StopAsyncWhileProcessing_DoesNotDeadLetter()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new BlockingWorker(started);

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker);
        await service.StartAsync(cts.Token);

        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "in-flight-item" }, cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await service.StopAsync(CancellationToken.None);

        dlq.Items.Should().BeEmpty();
        worker.Calls.Should().Be(1);
    }

    private static Channel<WorkflowWorkItem> CreateChannel() =>
        Channel.CreateBounded<WorkflowWorkItem>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

    private static (InMemoryWorkflowDispatcher dispatcher,
                    InMemoryWorkflowWorkerService service,
                    InMemoryDeadLetterSink dlq,
                    CancellationTokenSource cts)
        BuildPipeline(IWorkflowWorker worker)
    {
        var channel = CreateChannel();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;
        var dispatcher = new InMemoryWorkflowDispatcher(channel);
        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        return (dispatcher, service, dlq, cts);
    }

    private sealed class DelegatingWorker(Func<WorkflowWorkItem, WorkItemResult> handler) : IWorkflowWorker
    {
        public Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default) =>
            Task.FromResult(handler(item));
    }

    private sealed class BlockingWorker(TaskCompletionSource started) : IWorkflowWorker
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public async Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            started.TrySetResult();

            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return WorkItemResult.Success;
        }
    }
}
