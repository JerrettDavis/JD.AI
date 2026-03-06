using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace JD.AI.Workflows.Distributed.Redis;

/// <summary>
/// Options for connecting to Redis for distributed workflow dispatch.
/// </summary>
public sealed class RedisWorkflowOptions
{
    /// <summary>Redis connection string (e.g. <c>localhost:6379</c>).</summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Redis Stream key used as the main queue.</summary>
    public string StreamKey { get; set; } = "jdai:workflows";

    /// <summary>Consumer group name. All workers in the same group compete for messages.</summary>
    public string ConsumerGroup { get; set; } = "jdai-workers";

    /// <summary>Prefix for dead-letter entries.</summary>
    public string DeadLetterKey { get; set; } = "jdai:workflows:dlq";

    /// <summary>How long to block waiting for new messages (default 5 s).</summary>
    public TimeSpan ReadBlockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum messages fetched per read call.</summary>
    public int BatchSize { get; set; } = 10;
}

/// <summary>
/// Dispatches <see cref="WorkflowWorkItem"/> instances to a Redis Stream.
/// </summary>
public sealed class RedisWorkflowDispatcher : IWorkflowDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly RedisWorkflowOptions _options;

    /// <summary>Initializes the dispatcher.</summary>
    public RedisWorkflowDispatcher(IConnectionMultiplexer redis, RedisWorkflowOptions options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task DispatchAsync(WorkflowWorkItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var db = _redis.GetDatabase();
        var payload = JsonSerializer.Serialize(item, JsonOptions);

        await db.StreamAddAsync(
            _options.StreamKey,
            [new NameValueEntry("payload", payload)]).ConfigureAwait(false);
    }
}

/// <summary>
/// Reads from a Redis Stream consumer group and delegates execution to <see cref="IWorkflowWorker"/>.
/// Dead-letters items that exceed <see cref="WorkflowWorkItem.MaxDeliveryCount"/>.
/// </summary>
public sealed class RedisWorkflowWorkerService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly IWorkflowWorker _worker;
    private readonly IDeadLetterSink _dlq;
    private readonly RedisWorkflowOptions _options;
    private readonly ILogger<RedisWorkflowWorkerService> _logger;
    private readonly string _consumerId = $"worker-{Guid.NewGuid():N}";

    /// <summary>Initializes the worker service.</summary>
    public RedisWorkflowWorkerService(
        IConnectionMultiplexer redis,
        IWorkflowWorker worker,
        IDeadLetterSink dlq,
        RedisWorkflowOptions options,
        ILogger<RedisWorkflowWorkerService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _dlq = dlq ?? throw new ArgumentNullException(nameof(dlq));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        // Ensure stream and consumer group exist
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                _options.StreamKey,
                _options.ConsumerGroup,
                StreamPosition.NewMessages).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — expected on restart
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    _options.StreamKey,
                    _options.ConsumerGroup,
                    _consumerId,
                    count: _options.BatchSize,
                    noAck: false)
                    .ConfigureAwait(false);

                if (entries.Length == 0)
                {
                    await Task.Delay(_options.ReadBlockTimeout, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessEntryAsync(db, entry, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from Redis Stream '{Key}'.", _options.StreamKey);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, StreamEntry entry, CancellationToken ct)
    {
        WorkflowWorkItem? item = null;
        try
        {
            var payload = (string?)entry["payload"];
            if (payload is null)
            {
                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
                return;
            }

            item = JsonSerializer.Deserialize<WorkflowWorkItem>(payload, JsonOptions);
            if (item is null)
            {
                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
                return;
            }

            item = item with { DeliveryCount = item.DeliveryCount + 1 };

            if (item.DeliveryCount > item.MaxDeliveryCount)
            {
                _logger.LogWarning("Workflow {Name}/{Id} exceeded max delivery count. Dead-lettering.", item.WorkflowName, item.Id);
                await _dlq.DeadLetterAsync(item, "Exceeded max delivery count", ct: ct).ConfigureAwait(false);
                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
                return;
            }

            var result = await _worker.ProcessAsync(item, ct).ConfigureAwait(false);

            if (result == WorkItemResult.Success)
            {
                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
            }
            else if (result == WorkItemResult.Permanent)
            {
                await _dlq.DeadLetterAsync(item, "Permanent failure", ct: ct).ConfigureAwait(false);
                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
            }
            // Transient: do not ACK; message remains pending for retry via XPENDING/XCLAIM
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stream entry {Id}.", entry.Id);
            if (item is not null)
            {
                await _dlq.DeadLetterAsync(item, "Unhandled exception", ex, ct).ConfigureAwait(false);
                await db.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>Dead-letter sink that writes failed items to a Redis List.</summary>
public sealed class RedisDeadLetterSink : IDeadLetterSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly RedisWorkflowOptions _options;

    /// <summary>Initializes the sink.</summary>
    public RedisDeadLetterSink(IConnectionMultiplexer redis, RedisWorkflowOptions options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task DeadLetterAsync(WorkflowWorkItem item, string reason, Exception? exception = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var entry = JsonSerializer.Serialize(new
        {
            item,
            reason,
            error = exception?.Message,
            deadLetteredAt = DateTimeOffset.UtcNow,
        }, JsonOptions);

        await db.ListRightPushAsync(_options.DeadLetterKey, entry).ConfigureAwait(false);
    }
}

/// <summary>DI extensions for the Redis transport.</summary>
public static class RedisWorkflowExtensions
{
    /// <summary>
    /// Registers Redis-backed dispatcher, worker service, and dead-letter sink.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configuration.</param>
    public static IServiceCollection AddRedisWorkflowDispatcher(
        this IServiceCollection services,
        Action<RedisWorkflowOptions>? configure = null)
    {
        var options = new RedisWorkflowOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(options.ConnectionString));
        services.AddSingleton<IWorkflowDispatcher, RedisWorkflowDispatcher>();
        services.AddSingleton<IDeadLetterSink, RedisDeadLetterSink>();
        services.AddHostedService<RedisWorkflowWorkerService>();

        return services;
    }
}
