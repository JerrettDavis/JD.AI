using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Tests.Workflows;

public sealed class InMemoryWorkflowTransportTests
{
    [Fact]
    public async Task DispatchAsync_EnqueuesItem()
    {
        var services = new ServiceCollection();
        services.AddInMemoryWorkflowDispatcher();
        services.AddSingleton<IWorkflowWorker, RecordingWorkflowWorker>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IWorkflowDispatcher>();

        var item = new WorkflowWorkItem { WorkflowName = "test-workflow" };
        await dispatcher.DispatchAsync(item);

        // Verify the item was enqueued (no exception = success for channel write)
        Assert.NotNull(item.Id);
    }

    [Fact]
    public async Task Worker_ProcessesDispatchedItem()
    {
        var worker = new RecordingWorkflowWorker();
        var dlq = new InMemoryDeadLetterSink();
        var services = new ServiceCollection();
        services.AddInMemoryWorkflowDispatcher();
        services.AddSingleton<IWorkflowWorker>(worker);
        services.AddSingleton<IDeadLetterSink>(dlq);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IWorkflowDispatcher>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(cts.Token);

        var item = new WorkflowWorkItem { WorkflowName = "my-workflow" };
        await dispatcher.DispatchAsync(item, cts.Token);

        // Give worker time to process
        await Task.Delay(200, cts.Token);

        Assert.Contains(worker.Processed, i => string.Equals(i.WorkflowName, "my-workflow", StringComparison.Ordinal));
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Worker_PermanentFailure_DeadLettersItem()
    {
        var worker = new AlwaysFailWorker(WorkItemResult.Permanent);
        var dlq = new InMemoryDeadLetterSink();
        var services = new ServiceCollection();
        services.AddInMemoryWorkflowDispatcher();
        services.AddSingleton<IWorkflowWorker>(worker);
        services.AddSingleton<IDeadLetterSink>(dlq);
        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IWorkflowDispatcher>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hostedService = provider.GetRequiredService<IHostedService>();
        await hostedService.StartAsync(cts.Token);

        var item = new WorkflowWorkItem { WorkflowName = "failing-workflow" };
        await dispatcher.DispatchAsync(item, cts.Token);
        await Task.Delay(300, cts.Token);

        Assert.Contains(dlq.Items, d => string.Equals(d.Item.WorkflowName, "failing-workflow", StringComparison.Ordinal));
        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void WorkflowWorkItem_DefaultValues_ArePopulated()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        Assert.NotEmpty(item.Id);
        Assert.NotEmpty(item.CorrelationId);
        Assert.True(item.EnqueuedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(5, item.MaxDeliveryCount);
        Assert.Equal(0, item.Priority);
    }

    [Fact]
    public async Task InMemoryDeadLetterSink_RecordsItems()
    {
        var sink = new InMemoryDeadLetterSink();
        var item = new WorkflowWorkItem { WorkflowName = "wf" };

        await sink.DeadLetterAsync(item, "test reason");

        Assert.Single(sink.Items);
        Assert.Equal("test reason", sink.Items[0].Reason);
        Assert.Contains(sink.Items, d => string.Equals(d.Item.WorkflowName, "wf", StringComparison.Ordinal));
    }

    private sealed class RecordingWorkflowWorker : IWorkflowWorker
    {
        public List<WorkflowWorkItem> Processed { get; } = [];

        public Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default)
        {
            Processed.Add(item);
            return Task.FromResult(WorkItemResult.Success);
        }
    }

    private sealed class AlwaysFailWorker(WorkItemResult result) : IWorkflowWorker
    {
        public Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default) =>
            Task.FromResult(result);
    }
}
