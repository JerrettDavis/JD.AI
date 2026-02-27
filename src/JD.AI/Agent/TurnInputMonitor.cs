using JD.AI.Tui.Rendering;

namespace JD.AI.Tui.Agent;

/// <summary>
/// Monitors keystrokes during an agent turn to support:
/// 1. Double-tap ESC to cancel the current turn
/// 2. Type-ahead steering — type and press Enter to queue a follow-up message
///
/// Keystroke capture is silent (intercept: true) so it doesn't interleave
/// with streaming output. Queued steering messages are processed after the
/// current turn completes.
/// </summary>
public sealed class TurnInputMonitor : IDisposable
{
    private readonly CancellationTokenSource _turnCts;
    private readonly Task _monitorTask;
    private readonly TimeSpan _doubleTapWindow;
    private readonly List<char> _steerBuffer = [];
    private volatile string? _steeringMessage;

    private const int PollIntervalMs = 50;

    public TurnInputMonitor(
        CancellationToken appToken,
        TimeSpan? doubleTapWindow = null)
    {
        _doubleTapWindow = doubleTapWindow ?? TimeSpan.FromMilliseconds(1500);
        _turnCts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
        _monitorTask = Task.Run(MonitorLoop);
    }

    /// <summary>
    /// Token that becomes cancelled on double-ESC or Ctrl+C.
    /// </summary>
    public CancellationToken Token => _turnCts.Token;

    /// <summary>
    /// The steering message typed and submitted (Enter) during the turn, or null.
    /// </summary>
    public string? SteeringMessage => _steeringMessage;

    private void MonitorLoop()
    {
        if (Console.IsInputRedirected) return;

        try
        {
            while (!_turnCts.IsCancellationRequested)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(PollIntervalMs);
                    continue;
                }

                var key = Console.ReadKey(intercept: true);

                // ── ESC handling ──
                if (key.Key == ConsoleKey.Escape)
                {
                    HandleEscape();
                    continue;
                }

                // ── Steering: Enter submits the buffer ──
                if (key.Key == ConsoleKey.Enter && _steerBuffer.Count > 0)
                {
                    _steeringMessage = new string([.. _steerBuffer]);
                    _steerBuffer.Clear();
                    Console.WriteLine();
                    ChatRenderer.RenderInfo($"  ▸ Queued: {_steeringMessage}");
                    continue;
                }

                // ── Steering: Backspace ──
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (_steerBuffer.Count > 0)
                        _steerBuffer.RemoveAt(_steerBuffer.Count - 1);
                    continue;
                }

                // ── Steering: accumulate printable characters ──
                if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                {
                    _steerBuffer.Add(key.KeyChar);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the turn completes
        }
        catch (InvalidOperationException)
        {
            // Console not available (redirected mid-operation)
        }
    }

    private void HandleEscape()
    {
        // If user was typing a steer message, first ESC clears it
        if (_steerBuffer.Count > 0)
        {
            _steerBuffer.Clear();
            ChatRenderer.RenderInfo("  (input cleared)");
            return;
        }

        Console.WriteLine();
        ChatRenderer.RenderWarning("Hit ESC again to cancel...");

        var deadline = DateTime.UtcNow + _doubleTapWindow;
        while (DateTime.UtcNow < deadline && !_turnCts.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(PollIntervalMs);
                continue;
            }

            var second = Console.ReadKey(intercept: true);
            if (second.Key == ConsoleKey.Escape)
            {
                ChatRenderer.RenderWarning("Cancelling...");
                _turnCts.Cancel();
                return;
            }
        }

        ChatRenderer.RenderInfo("  (cancel aborted)");
    }

    public void Dispose()
    {
        if (!_turnCts.IsCancellationRequested)
            _turnCts.Cancel();

        _monitorTask.Wait(TimeSpan.FromMilliseconds(300));
        _turnCts.Dispose();
    }
}
