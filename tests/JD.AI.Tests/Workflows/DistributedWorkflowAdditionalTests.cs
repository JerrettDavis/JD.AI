using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.AzureServiceBus;
using JD.AI.Workflows.Distributed.InMemory;
using JD.AI.Workflows.Distributed.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JD.AI.Tests.Workflows;

/// <summary>
/// Additional tests for distributed workflow transport components.
/// Covers options defaults, DI extension registrations, and WorkflowWorkItem behavior.
/// </summary>
public sealed class DistributedWorkflowAdditionalTests
{
    // ──────────────────────────────────────────────────────────────────────
    // WorkflowWorkItem
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void WorkflowWorkItem_UniqueIds_AcrossInstances()
    {
        var a = new WorkflowWorkItem { WorkflowName = "wf" };
        var b = new WorkflowWorkItem { WorkflowName = "wf" };
        Assert.NotEqual(a.Id, b.Id, StringComparer.Ordinal);
        Assert.NotEqual(a.CorrelationId, b.CorrelationId, StringComparer.Ordinal);
    }

    [Fact]
    public void WorkflowWorkItem_WithRecord_PreservesId()
    {
        var original = new WorkflowWorkItem { WorkflowName = "wf" };
        var updated = original with { DeliveryCount = original.DeliveryCount + 1 };
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(1, updated.DeliveryCount);
    }

    [Fact]
    public void WorkflowWorkItem_InitialContext_IsEmpty_ByDefault()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        Assert.NotNull(item.InitialContext);
        Assert.Empty(item.InitialContext);
    }

    [Fact]
    public void WorkflowWorkItem_InitialContext_CanBeSet()
    {
        var item = new WorkflowWorkItem
        {
            WorkflowName = "wf",
            InitialContext = new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "value" },
        };
        Assert.Single(item.InitialContext);
        Assert.Equal("value", item.InitialContext["key"]);
    }

    [Fact]
    public void WorkflowWorkItem_CanBeInitializedWithCustomValues()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new WorkflowWorkItem
        {
            WorkflowName = "my-workflow",
            Priority = 5,
            MaxDeliveryCount = 3,
            EnqueuedAt = now,
        };

        Assert.Equal("my-workflow", item.WorkflowName);
        Assert.Equal(5, item.Priority);
        Assert.Equal(3, item.MaxDeliveryCount);
        Assert.Equal(now, item.EnqueuedAt);
    }

    // ──────────────────────────────────────────────────────────────────────
    // WorkItemResult
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void WorkItemResult_SuccessValue_IsSuccess()
    {
        Assert.Equal(WorkItemResult.Success, WorkItemResult.Success);
    }

    [Fact]
    public void WorkItemResult_PermanentValue_IsPermanent()
    {
        Assert.Equal(WorkItemResult.Permanent, WorkItemResult.Permanent);
    }

    [Fact]
    public void WorkItemResult_TransientValue_IsTransient()
    {
        Assert.Equal(WorkItemResult.Transient, WorkItemResult.Transient);
    }

    // ──────────────────────────────────────────────────────────────────────
    // InMemoryDeadLetterSink
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InMemoryDeadLetterSink_RecordsMultipleItems()
    {
        var sink = new InMemoryDeadLetterSink();
        var item1 = new WorkflowWorkItem { WorkflowName = "wf1" };
        var item2 = new WorkflowWorkItem { WorkflowName = "wf2" };

        await sink.DeadLetterAsync(item1, "reason1");
        await sink.DeadLetterAsync(item2, "reason2", new InvalidOperationException("oops"));

        Assert.Equal(2, sink.Items.Count);
        Assert.NotNull(sink.Items[1].Exception);
        Assert.Equal("reason1", sink.Items[0].Reason);
        Assert.Equal("reason2", sink.Items[1].Reason);
    }

    [Fact]
    public void DeadLetteredItem_Properties_AreSet()
    {
        var item = new WorkflowWorkItem { WorkflowName = "wf" };
        var ex = new InvalidOperationException("err");
        var now = DateTimeOffset.UtcNow;

        var dead = new DeadLetteredItem(item, "reason", ex, now);
        Assert.Same(item, dead.Item);
        Assert.Equal("reason", dead.Reason);
        Assert.Same(ex, dead.Exception);
        Assert.Equal(now, dead.DeadLetteredAt);
    }

    // ──────────────────────────────────────────────────────────────────────
    // InMemory DI extensions
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersDispatcher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkflowWorker>(new NoOpWorker());
        services.AddInMemoryWorkflowDispatcher(capacity: 100);

        await using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetService<IWorkflowDispatcher>();
        Assert.NotNull(dispatcher);
        Assert.IsType<InMemoryWorkflowDispatcher>(dispatcher);
    }

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersDeadLetterSink()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkflowWorker>(new NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();
        var sink = sp.GetService<IDeadLetterSink>();
        Assert.NotNull(sink);
        Assert.IsType<InMemoryDeadLetterSink>(sink);
    }

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkflowWorker>(new NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();
        var hostedServices = sp.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s is InMemoryWorkflowWorkerService);
    }

    // ──────────────────────────────────────────────────────────────────────
    // ServiceBus options defaults
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ServiceBusWorkflowOptions_Defaults_AreSet()
    {
        var opts = new ServiceBusWorkflowOptions();
        Assert.Empty(opts.ConnectionString);
        Assert.NotEmpty(opts.QueueName);
        Assert.NotEmpty(opts.DeadLetterQueueName);
        Assert.True(opts.MaxConcurrentCalls >= 1);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Redis options defaults
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void RedisWorkflowOptions_Defaults_AreSet()
    {
        var opts = new RedisWorkflowOptions();
        Assert.NotEmpty(opts.StreamKey);
        Assert.NotEmpty(opts.ConsumerGroup);
        Assert.NotEmpty(opts.DeadLetterKey);
        Assert.True(opts.ReadBlockTimeout > TimeSpan.Zero);
        Assert.True(opts.BatchSize >= 1);
    }

    private sealed class NoOpWorker : IWorkflowWorker
    {
        public Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default)
            => Task.FromResult(WorkItemResult.Success);
    }
}
