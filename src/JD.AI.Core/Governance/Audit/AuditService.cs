using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JD.AI.Core.Governance.Audit;

/// <summary>
/// Dispatches <see cref="AuditEvent"/> instances to all registered <see cref="IAuditSink"/>
/// implementations. A failure in one sink never propagates to callers; failed events are
/// sent to a dead-letter queue for later inspection.
/// </summary>
public sealed class AuditService
{
    private readonly IReadOnlyList<IAuditSink> _sinks;
    private readonly ILogger<AuditService> _logger;
    private readonly Channel<DeadLetterEntry> _deadLetterQueue;
    private long _deadLetterCount;

    public AuditService(IEnumerable<IAuditSink> sinks, ILogger<AuditService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
        _logger = logger ?? NullLogger<AuditService>.Instance;
        _deadLetterQueue = Channel.CreateBounded<DeadLetterEntry>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false,
            });
    }

    /// <summary>Number of events that failed to write to at least one sink.</summary>
    public long DeadLetterCount => Interlocked.Read(ref _deadLetterCount);

    /// <summary>
    /// Emits an audit event to all configured sinks. Exceptions from individual
    /// sinks are caught and logged; failed events are sent to the dead-letter queue.
    /// </summary>
    public async Task EmitAsync(AuditEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.WriteAsync(evt, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit sink '{SinkName}' failed for event {EventId}",
                    sink.Name, evt.EventId);
                EnqueueDeadLetter(evt, sink.Name, ex);
            }
        }
    }

    /// <summary>Flushes all sinks. Sink flush failures are logged but not propagated.</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                await sink.FlushAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit sink '{SinkName}' flush failed", sink.Name);
            }
        }
    }

    /// <summary>Reads all dead-letter entries currently in the queue.</summary>
    public IReadOnlyList<DeadLetterEntry> DrainDeadLetters()
    {
        var entries = new List<DeadLetterEntry>();
        while (_deadLetterQueue.Reader.TryRead(out var entry))
            entries.Add(entry);
        return entries;
    }

    private void EnqueueDeadLetter(AuditEvent evt, string sinkName, Exception ex)
    {
        Interlocked.Increment(ref _deadLetterCount);
        var entry = new DeadLetterEntry(evt, sinkName, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow);
        _deadLetterQueue.Writer.TryWrite(entry);
    }
}

/// <summary>A failed audit write that was caught and queued for inspection.</summary>
public sealed record DeadLetterEntry(
    AuditEvent Event,
    string SinkName,
    string ErrorType,
    string ErrorMessage,
    DateTimeOffset FailedAt);
