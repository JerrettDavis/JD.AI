using System.Threading.Channels;
using FluentAssertions;
using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// End-to-end integration tests for the in-memory transport pipeline:
/// dispatcher → channel → worker service → dead-letter sink.
/// All components are wired without real external services.
/// </summary>
public sealed class InMemoryEndToEndTests
{
    // ── Success pipeline ──────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_SuccessfulItem_WorkerCalledAndNoDlq()
    {
        var recorded = new List<WorkflowWorkItem>();
        var worker = new DelegatingWorker(item =>
        {
            recorded.Add(item);
            return WorkItemResult.Success;
        });

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker);
        await service.StartAsync(cts.Token);

        var workItem = new WorkflowWorkItem { WorkflowName = "success-pipeline" };
        await dispatcher.DispatchAsync(workItem, cts.Token);
        await Task.Delay(300, cts.Token);

        recorded.Should().ContainSingle(i => i.WorkflowName == "success-pipeline");
        dlq.Items.Should().BeEmpty();

        await service.StopAsync(CancellationToken.None);
    }

    // ── Permanent failure pipeline ────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_PermanentFailure_ItemDeadLettered()
    {
        var worker = new DelegatingWorker(_ => WorkItemResult.Permanent);

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker);
        await service.StartAsync(cts.Token);

        var workItem = new WorkflowWorkItem { WorkflowName = "permanent-pipeline" };
        await dispatcher.DispatchAsync(workItem, cts.Token);
        await Task.Delay(300, cts.Token);

        dlq.Items.Should().ContainSingle(d => d.Item.WorkflowName == "permanent-pipeline");
        dlq.Items[0].Reason.Should().Contain("Permanent");

        await service.StopAsync(CancellationToken.None);
    }

    // ── Exception pipeline ────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_WorkerThrows_ItemDeadLetteredWithException()
    {
        var expectedException = new InvalidOperationException("pipeline error");
        var worker = new ThrowingWorker(expectedException);

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker);
        await service.StartAsync(cts.Token);

        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "throwing-pipeline" }, cts.Token);
        await Task.Delay(300, cts.Token);

        dlq.Items.Should().ContainSingle();
        dlq.Items[0].Exception.Should().Be(expectedException);

        await service.StopAsync(CancellationToken.None);
    }

    // ── High volume ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_ManyItems_AllProcessed()
    {
        const int Count = 20;
        var processed = new System.Collections.Concurrent.ConcurrentBag<string>();

        var worker = new DelegatingWorker(item =>
        {
            processed.Add(item.WorkflowName);
            return WorkItemResult.Success;
        });

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker, capacity: Count * 2);
        await service.StartAsync(cts.Token);

        for (var i = 0; i < Count; i++)
            await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = $"bulk-wf-{i}" }, cts.Token);

        await Task.Delay(1000, cts.Token);

        processed.Should().HaveCount(Count);
        dlq.Items.Should().BeEmpty();

        await service.StopAsync(CancellationToken.None);
    }

    // ── Mixed results ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_MixedResults_OnlyPermanentFailuresDeadLettered()
    {
        var callCount = 0;
        var worker = new DelegatingWorker(item =>
        {
            var n = System.Threading.Interlocked.Increment(ref callCount);
            return n switch
            {
                1 => WorkItemResult.Success,
                2 => WorkItemResult.Permanent,
                3 => WorkItemResult.Transient,
                _ => WorkItemResult.Success,
            };
        });

        var (dispatcher, service, dlq, cts) = BuildPipeline(worker);
        await service.StartAsync(cts.Token);

        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "wf-success" }, cts.Token);
        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "wf-permanent" }, cts.Token);
        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "wf-transient" }, cts.Token);
        await Task.Delay(500, cts.Token);

        dlq.Items.Should().ContainSingle(d => d.Item.WorkflowName == "wf-permanent");

        await service.StopAsync(CancellationToken.None);
    }

    // ── DI-based wiring ───────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_DiWiring_WorksCorrectly()
    {
        var recordingWorker = new DelegatingWorker(_ => WorkItemResult.Success);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkflowWorker>(recordingWorker);
        services.AddInMemoryWorkflowDispatcher(capacity: 100);

        await using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IWorkflowDispatcher>();
        var hostedService = sp.GetRequiredService<IHostedService>();
        var dlq = (InMemoryDeadLetterSink)sp.GetRequiredService<IDeadLetterSink>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await hostedService.StartAsync(cts.Token);

        await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "di-wired-wf" }, cts.Token);
        await Task.Delay(300, cts.Token);

        dlq.Items.Should().BeEmpty();

        await hostedService.StopAsync(CancellationToken.None);
    }

    // ── Dispatcher disposal before service stop ────────────────────────────────

    [Fact]
    public async Task EndToEnd_DispatcherDisposed_ServiceDrainsAndStops()
    {
        var processed = new List<WorkflowWorkItem>();
        var worker = new DelegatingWorker(item =>
        {
            processed.Add(item);
            return WorkItemResult.Success;
        });

        var channel = Channel.CreateUnbounded<WorkflowWorkItem>();
        var dlq = new InMemoryDeadLetterSink();
        var logger = NullLogger<InMemoryWorkflowWorkerService>.Instance;
        var dispatcher = new InMemoryWorkflowDispatcher(channel);
        var service = new InMemoryWorkflowWorkerService(channel, worker, dlq, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);

        for (var i = 0; i < 5; i++)
            await dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = $"drain-wf-{i}" }, cts.Token);

        // Complete the channel — this tells the reader no more items are coming
        await dispatcher.DisposeAsync();

        // Allow time to drain
        await Task.Delay(500, cts.Token);

        processed.Should().HaveCount(5);
        await service.StopAsync(CancellationToken.None);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (InMemoryWorkflowDispatcher dispatcher,
                    InMemoryWorkflowWorkerService service,
                    InMemoryDeadLetterSink dlq,
                    CancellationTokenSource cts)
        BuildPipeline(IWorkflowWorker worker, int capacity = 1_000)
    {
        var channel = Channel.CreateBounded<WorkflowWorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

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

    private sealed class ThrowingWorker(Exception ex) : IWorkflowWorker
    {
        public Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default) =>
            throw ex;
    }
}
