using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JD.AI.Workflows.Distributed.InMemory;

/// <summary>
/// In-process dispatcher and worker backed by a bounded <see cref="Channel{T}"/>.
/// Suitable for local development, testing, and single-process deployments.
/// </summary>
public sealed class InMemoryWorkflowDispatcher : IWorkflowDispatcher, IAsyncDisposable
{
    private readonly Channel<WorkflowWorkItem> _channel;

    /// <summary>Initializes the dispatcher with the given channel.</summary>
    public InMemoryWorkflowDispatcher(Channel<WorkflowWorkItem> channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(WorkflowWorkItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
    }

    /// <summary>Completes the channel, signalling no further items will be written.</summary>
    public ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// In-process worker that reads from the shared channel and delegates to a registered
/// <see cref="IWorkflowWorker"/> implementation.
/// </summary>
public sealed class InMemoryWorkflowWorkerService : BackgroundService
{
    private readonly Channel<WorkflowWorkItem> _channel;
    private readonly IWorkflowWorker _worker;
    private readonly IDeadLetterSink _dlq;
    private readonly ILogger<InMemoryWorkflowWorkerService> _logger;

    /// <summary>Initializes the background service.</summary>
    public InMemoryWorkflowWorkerService(
        Channel<WorkflowWorkItem> channel,
        IWorkflowWorker worker,
        IDeadLetterSink dlq,
        ILogger<InMemoryWorkflowWorkerService> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _dlq = dlq ?? throw new ArgumentNullException(nameof(dlq));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var result = await _worker.ProcessAsync(item, stoppingToken).ConfigureAwait(false);

                if (result == WorkItemResult.Permanent)
                {
                    _logger.LogWarning("Workflow {Name}/{Id} permanently failed. Dead-lettering.", item.WorkflowName, item.Id);
                    await _dlq.DeadLetterAsync(item, "Permanent failure", ct: stoppingToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing workflow {Name}/{Id}.", item.WorkflowName, item.Id);
                await _dlq.DeadLetterAsync(item, "Unhandled exception", ex, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// In-memory dead-letter sink that retains failed items in memory for inspection.
/// </summary>
public sealed class InMemoryDeadLetterSink : IDeadLetterSink
{
    private readonly List<DeadLetteredItem> _items = [];
    private readonly Lock _lock = new();

    /// <summary>All dead-lettered items in insertion order.</summary>
    public IReadOnlyList<DeadLetteredItem> Items
    {
        get
        {
            lock (_lock) return _items.ToArray();
        }
    }

    /// <inheritdoc/>
    public Task DeadLetterAsync(WorkflowWorkItem item, string reason, Exception? exception = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _items.Add(new DeadLetteredItem(item, reason, exception, DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }
}

/// <summary>A dead-lettered work item with diagnostic metadata.</summary>
public sealed record DeadLetteredItem(
    WorkflowWorkItem Item,
    string Reason,
    Exception? Exception,
    DateTimeOffset DeadLetteredAt);

/// <summary>DI extensions for the in-memory transport.</summary>
public static class InMemoryWorkflowExtensions
{
    /// <summary>
    /// Registers the in-memory channel-based dispatcher and worker service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="capacity">Bounded channel capacity (default 1,000).</param>
    public static IServiceCollection AddInMemoryWorkflowDispatcher(
        this IServiceCollection services,
        int capacity = 1_000)
    {
        var channel = Channel.CreateBounded<WorkflowWorkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

        services.AddSingleton(channel);
        services.AddSingleton<IWorkflowDispatcher, InMemoryWorkflowDispatcher>();
        services.AddSingleton<IDeadLetterSink, InMemoryDeadLetterSink>();
        services.AddHostedService<InMemoryWorkflowWorkerService>();

        return services;
    }
}
