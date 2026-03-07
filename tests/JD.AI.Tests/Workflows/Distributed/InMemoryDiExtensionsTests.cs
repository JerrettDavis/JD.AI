using System.Threading.Channels;
using FluentAssertions;
using JD.AI.Workflows.Distributed;
using JD.AI.Workflows.Distributed.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JD.AI.Tests.Workflows.Distributed;

/// <summary>
/// Tests for InMemoryWorkflowExtensions.AddInMemoryWorkflowDispatcher,
/// verifying DI registration and configured types.
/// </summary>
public sealed class InMemoryDiExtensionsTests
{
    private static DelegatingWorker NoOpWorker() => new DelegatingWorker(_ => WorkItemResult.Success);

    // ── Service registrations ─────────────────────────────────────────────────

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersIWorkflowDispatcher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();

        sp.GetService<IWorkflowDispatcher>().Should().NotBeNull()
            .And.BeOfType<InMemoryWorkflowDispatcher>();
    }

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersIDeadLetterSink()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();

        sp.GetService<IDeadLetterSink>().Should().NotBeNull()
            .And.BeOfType<InMemoryDeadLetterSink>();
    }

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWorkflowWorker>(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();

        var hostedServices = sp.GetServices<IHostedService>();
        hostedServices.Should().Contain(s => s is InMemoryWorkflowWorkerService);
    }

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_RegistersChannel()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher(capacity: 50);

        await using var sp = services.BuildServiceProvider();

        var channel = sp.GetService<Channel<WorkflowWorkItem>>();
        channel.Should().NotBeNull();
    }

    // ── Capacity parameter ────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public async Task AddInMemoryWorkflowDispatcher_VariousCapacities_ChannelIsUsable(int capacity)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher(capacity);

        await using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IWorkflowDispatcher>();

        // Should be able to dispatch at least one item
        Func<Task> act = () => dispatcher.DispatchAsync(new WorkflowWorkItem { WorkflowName = "wf" });

        await act.Should().NotThrowAsync();
    }

    // ── Default capacity ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_DefaultCapacity_IsUsable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher(); // default capacity = 1,000

        await using var sp = services.BuildServiceProvider();

        sp.GetService<IWorkflowDispatcher>().Should().NotBeNull();
    }

    // ── Singleton lifetime ────────────────────────────────────────────────────

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_DispatcherIsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();

        var d1 = sp.GetRequiredService<IWorkflowDispatcher>();
        var d2 = sp.GetRequiredService<IWorkflowDispatcher>();

        d1.Should().BeSameAs(d2);
    }

    [Fact]
    public async Task AddInMemoryWorkflowDispatcher_DeadLetterSinkIsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NoOpWorker());
        services.AddInMemoryWorkflowDispatcher();

        await using var sp = services.BuildServiceProvider();

        var s1 = sp.GetRequiredService<IDeadLetterSink>();
        var s2 = sp.GetRequiredService<IDeadLetterSink>();

        s1.Should().BeSameAs(s2);
    }

    // ── Returns IServiceCollection (fluent chaining) ──────────────────────────

    [Fact]
    public void AddInMemoryWorkflowDispatcher_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddInMemoryWorkflowDispatcher();

        returned.Should().BeSameAs(services);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class DelegatingWorker(Func<WorkflowWorkItem, WorkItemResult> handler) : IWorkflowWorker
    {
        public Task<WorkItemResult> ProcessAsync(WorkflowWorkItem item, CancellationToken ct = default) =>
            Task.FromResult(handler(item));
    }
}
