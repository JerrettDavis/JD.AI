using JD.AI.Core.Channels;
using JD.AI.Core.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JD.AI.Channels.Queue;

/// <summary>
/// Decorator that adds a durable SQLite WAL queue in front of any <see cref="IChannel"/>.
/// Inbound messages are immediately written to the queue and acknowledged to Discord;
/// a background worker drains the queue and dispatches to registered handlers.
///
/// This means Discord messages are never lost even if the agent or gateway restarts,
/// and burst traffic is buffered safely on disk.
/// </summary>
public sealed class DurableQueueChannelDecorator
    : IChannel, ICommandAwareChannel, IHostedService
{
    private readonly IChannel _inner;
    private readonly DiscordMessageBuffer _queue;
    private readonly ILogger _log;
    private Func<ChannelMessage, Task>? _messageReceived;
    private bool _innerHandlerRegistered;
    private CancellationTokenSource? _cts;

    public DurableQueueChannelDecorator(
        IChannel inner,
        DiscordMessageBuffer queue,
        ILogger logger)
    {
        _inner = inner;
        _queue = queue;
        _log = logger;
    }

    public string ChannelType => _inner.ChannelType;
    public string DisplayName => _inner.DisplayName;
    public bool IsConnected => _inner.IsConnected;

    /// <inheritdoc />
    public event Func<ChannelMessage, Task>? MessageReceived
    {
        add
        {
            _messageReceived += value;
            if (!_innerHandlerRegistered && value is not null)
            {
                // Wire inner channel → queue → dispatcher chain once.
                _inner.MessageReceived += InnerMessageReceived;
                _innerHandlerRegistered = true;
            }
        }
        remove
        {
            _messageReceived -= value;
        }
    }

    /// <summary>
    /// Registers admin commands for queue inspection and management.
    /// Call this during gateway startup after <see cref="ICommandRegistry"/> is populated.
    /// </summary>
    public Task RegisterCommandsAsync(ICommandRegistry registry, CancellationToken ct = default)
    {
        registry.Register(new QueuePeekCommand(_queue));
        registry.Register(new QueueRetryCommand(_queue));
        registry.Register(new QueuePurgeCommand(_queue));
        return Task.CompletedTask;
    }

    /// <summary>Implements <c>IHostedService.StartAsync</c> — starts the background queue drainer.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Initialize the SQLite WAL DB before the drainer starts processing.
        // Idempotent — safe to call even if already initialized.
        await _queue.InitializeAsync(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _log.LogInformation("DurableQueueChannelDecorator background worker starting for {ChannelType}", ChannelType);
        _ = DrainQueueAsync(_cts.Token); // fire-and-forget worker
    }

    /// <summary>Implements <c>IHostedService.StopAsync</c>.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _log.LogInformation("DurableQueueChannelDecorator background worker stopping for {ChannelType}", ChannelType);
        _cts?.Cancel();
        try { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); } catch { /* best-effort */ }
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default) =>
        _inner.ConnectAsync(ct);

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        return _inner.DisconnectAsync(ct);
    }

    /// <inheritdoc />
    public Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default) =>
        _inner.SendMessageAsync(conversationId, content, ct);

    /// <inheritdoc />
    public Task SendMessageWithAttachmentsAsync(
        string conversationId,
        string? content,
        IReadOnlyList<OutboundAttachment> attachments,
        CancellationToken ct = default) =>
        _inner.SendMessageWithAttachmentsAsync(conversationId, content, attachments, ct);

    /// <inheritdoc />
    public Task ReactAsync(string conversationId, string messageId, string emoji, CancellationToken ct = default) =>
        _inner.ReactAsync(conversationId, messageId, emoji, ct);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        await _inner.DisposeAsync();
        await _queue.DisposeAsync();
    }

    /// <summary>
    /// Enqueues the incoming message to SQLite and returns immediately.
    /// </summary>
    private async Task InnerMessageReceived(ChannelMessage msg)
    {
        try
        {
            await _queue.EnqueueAsync(msg);
        }
        catch
        {
            // Enqueue failed — fall back to direct invoke (best-effort)
            if (_messageReceived is not null)
                await _messageReceived.Invoke(msg);
        }
    }

    /// <summary>
    /// Background loop: continuously dequeues messages and dispatches to handlers.
    /// </summary>
    private async Task DrainQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msg = await _queue.DequeueAsync(ct);
                if (msg is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    continue;
                }

                _log.LogDebug("Queue drain: {MessageId} (attempt {Attempt}/{Max})",
                    msg.MessageId, msg.AttemptCount, QueuedChannelMessage.MaxAttempts);

                try
                {
                    if (_messageReceived is not null)
                        await _messageReceived.Invoke(msg.ToChannelMessage());
                    await _queue.CompleteAsync(msg.RowId, ct);
                }
                catch (Exception ex) when (msg.AttemptCount < QueuedChannelMessage.MaxAttempts)
                {
                    _log.LogWarning(ex, "Transient failure for {MessageId} — scheduling retry", msg.MessageId);
                    await _queue.RetryAsync(msg.RowId, ex.Message, ct);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Permanent failure for {MessageId} — marking failed", msg.MessageId);
                    await _queue.FailAsync(msg.RowId, ex.Message, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Queue drain error — continuing after pause");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
