using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JD.AI.Workflows.Distributed.AzureServiceBus;

/// <summary>
/// Options for Azure Service Bus workflow dispatch.
/// </summary>
public sealed class ServiceBusWorkflowOptions
{
    /// <summary>Azure Service Bus connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Main queue or topic name for workflow work items.</summary>
    public string QueueName { get; set; } = "jdai-workflows";

    /// <summary>Dead-letter queue name (default: built-in ASB DLQ).</summary>
    public string DeadLetterQueueName { get; set; } = "jdai-workflows/$DeadLetterQueue";

    /// <summary>Maximum concurrent message handlers (default 1).</summary>
    public int MaxConcurrentCalls { get; set; } = 1;
}

/// <summary>
/// Dispatches <see cref="WorkflowWorkItem"/> instances to Azure Service Bus.
/// </summary>
public sealed class ServiceBusWorkflowDispatcher : IWorkflowDispatcher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusSender _sender;

    /// <summary>Initializes the dispatcher.</summary>
    public ServiceBusWorkflowDispatcher(ServiceBusClient client, ServiceBusWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _sender = client.CreateSender(options.QueueName);
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(WorkflowWorkItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var payload = JsonSerializer.Serialize(item, JsonOptions);
        var message = new ServiceBusMessage(payload)
        {
            MessageId = item.Id,
            CorrelationId = item.CorrelationId,
            Subject = item.WorkflowName,
        };

        await _sender.SendMessageAsync(message, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _sender.DisposeAsync().ConfigureAwait(false);
}

/// <summary>
/// Azure Service Bus processor worker that delegates to <see cref="IWorkflowWorker"/>.
/// </summary>
public sealed class ServiceBusWorkflowWorkerService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusProcessor _processor;
    private readonly IWorkflowWorker _worker;
    private readonly IDeadLetterSink _dlq;
    private readonly ILogger<ServiceBusWorkflowWorkerService> _logger;

    /// <summary>Initializes the worker service.</summary>
    public ServiceBusWorkflowWorkerService(
        ServiceBusClient client,
        IWorkflowWorker worker,
        IDeadLetterSink dlq,
        ServiceBusWorkflowOptions options,
        ILogger<ServiceBusWorkflowWorkerService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _dlq = dlq ?? throw new ArgumentNullException(nameof(dlq));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            AutoCompleteMessages = false,
        };
        _processor = client.CreateProcessor(options.QueueName, processorOptions);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            await _processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        WorkflowWorkItem? item = null;
        try
        {
            var payload = args.Message.Body.ToString();
            item = JsonSerializer.Deserialize<WorkflowWorkItem>(payload, JsonOptions);

            if (item is null)
            {
                await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Payload could not be deserialized.")
                    .ConfigureAwait(false);
                return;
            }

            var result = await _worker.ProcessAsync(item, args.CancellationToken).ConfigureAwait(false);

            switch (result)
            {
                case WorkItemResult.Success:
                    await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
                    break;

                case WorkItemResult.Permanent:
                    await _dlq.DeadLetterAsync(item, "Permanent failure", ct: args.CancellationToken).ConfigureAwait(false);
                    await args.DeadLetterMessageAsync(args.Message, "PermanentFailure", "Worker returned permanent failure.", args.CancellationToken).ConfigureAwait(false);
                    break;

                case WorkItemResult.Transient:
                    await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing Service Bus message {Id}.", args.Message.MessageId);
            if (item is not null)
            {
                await _dlq.DeadLetterAsync(item, "Unhandled exception", ex, args.CancellationToken).ConfigureAwait(false);
            }

            await args.DeadLetterMessageAsync(args.Message, "UnhandledException", ex.Message, args.CancellationToken).ConfigureAwait(false);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Azure Service Bus processor error in {Source}.", args.ErrorSource);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async void Dispose()
    {
        await _processor.DisposeAsync().ConfigureAwait(false);
        base.Dispose();
    }
}

/// <summary>Dead-letter sink that records items to the audit log (no-op default).</summary>
public sealed class ServiceBusDeadLetterSink : IDeadLetterSink
{
    private readonly ILogger<ServiceBusDeadLetterSink> _logger;

    /// <summary>Initializes the sink with a logger.</summary>
    public ServiceBusDeadLetterSink(ILogger<ServiceBusDeadLetterSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task DeadLetterAsync(WorkflowWorkItem item, string reason, Exception? exception = null, CancellationToken ct = default)
    {
        _logger.LogWarning(exception,
            "Workflow {Name}/{Id} dead-lettered. Reason: {Reason}",
            item.WorkflowName, item.Id, reason);
        return Task.CompletedTask;
    }
}

/// <summary>DI extensions for Azure Service Bus transport.</summary>
public static class ServiceBusWorkflowExtensions
{
    /// <summary>
    /// Registers the Azure Service Bus dispatcher and worker service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Options configuration callback.</param>
    public static IServiceCollection AddAzureServiceBusWorkflowDispatcher(
        this IServiceCollection services,
        Action<ServiceBusWorkflowOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ServiceBusWorkflowOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(_ => new ServiceBusClient(options.ConnectionString));
        services.AddSingleton<IWorkflowDispatcher, ServiceBusWorkflowDispatcher>();
        services.AddSingleton<IDeadLetterSink, ServiceBusDeadLetterSink>();
        services.AddHostedService<ServiceBusWorkflowWorkerService>();

        return services;
    }
}
