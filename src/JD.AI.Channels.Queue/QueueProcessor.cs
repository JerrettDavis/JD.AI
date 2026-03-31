using JD.AI.Core.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.Queue;

/// <summary>
/// Background worker that drains the durable queue and dispatches messages to the
/// registered <see cref="IChannel.MessageReceived"/> handler at a controlled rate.
/// </summary>
public sealed class QueueProcessor : BackgroundService
{
    private readonly DurableMessageQueue _queue;
    private readonly Func<ChannelMessage, Task> _dispatcher;
    private readonly ILogger<QueueProcessor> _log;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    public QueueProcessor(
        DurableMessageQueue queue,
        Func<ChannelMessage, Task> dispatcher,
        ILogger<QueueProcessor> log)
    {
        _queue = queue;
        _dispatcher = dispatcher;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("QueueProcessor starting — queue drain worker active");

        // Drain loop: claim one message, dispatch it, repeat.
        // If queue is empty we sleep briefly before repolling.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var msg = await _queue.DequeueAsync(stoppingToken);
                if (msg is null)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                _log.LogDebug(
                    "Dequeued message {MessageId} (attempt {Attempt}/{MaxAttempts}) from channel {ChannelId}",
                    msg.MessageId, msg.AttemptCount, QueuedChannelMessage.MaxAttempts, msg.ChannelId);

                try
                {
                    await _dispatcher(msg.ToChannelMessage());
                    await _queue.CompleteAsync(msg.RowId, stoppingToken);

                    _log.LogDebug("Message {MessageId} processed successfully", msg.MessageId);
                }
                catch (Exception ex) when (msg.AttemptCount < QueuedChannelMessage.MaxAttempts)
                {
                    _log.LogWarning(ex,
                        "Transient failure processing message {MessageId} (attempt {Attempt}) — scheduling retry",
                        msg.MessageId, msg.AttemptCount);
                    await _queue.RetryAsync(msg.RowId, ex.Message, stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex,
                        "Permanent failure for message {MessageId} after {Max} attempts — marking failed",
                        msg.MessageId, QueuedChannelMessage.MaxAttempts);
                    await _queue.FailAsync(msg.RowId, ex.Message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "QueueProcessor loop error — continuing after brief pause");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _log.LogInformation("QueueProcessor stopping");
    }
}
