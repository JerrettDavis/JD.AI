using JD.AI.Tui.Rendering;

namespace JD.AI.Tui.Agent;

/// <summary>
/// Monitors for double-tap ESC to cancel a running operation.
/// First ESC shows a warning; second ESC within the timeout triggers cancellation.
/// </summary>
public sealed class EscapeCancellation : IDisposable
{
    private readonly CancellationTokenSource _turnCts;
    private readonly CancellationToken _appToken;
    private readonly Task _monitorTask;
    private readonly TimeSpan _doubleTapWindow;

    /// <summary>
    /// Creates an escape-cancellation monitor for a single agent turn.
    /// </summary>
    /// <param name="appToken">The application-level cancellation token (Ctrl+C).</param>
    /// <param name="doubleTapWindow">
    /// Maximum time between the two ESC presses.
    /// Defaults to 1.5 seconds.
    /// </param>
    public EscapeCancellation(
        CancellationToken appToken,
        TimeSpan? doubleTapWindow = null)
    {
        _appToken = appToken;
        _doubleTapWindow = doubleTapWindow ?? TimeSpan.FromMilliseconds(1500);
        _turnCts = CancellationTokenSource.CreateLinkedTokenSource(appToken);
        _monitorTask = Task.Run(MonitorLoop);
    }

    /// <summary>
    /// Token that becomes cancelled on double-ESC or Ctrl+C.
    /// Pass this to the agent turn.
    /// </summary>
    public CancellationToken Token => _turnCts.Token;

    private void MonitorLoop()
    {
        // Don't monitor when stdin is redirected (e.g., tests, pipes)
        if (Console.IsInputRedirected) return;

        try
        {
            while (!_turnCts.IsCancellationRequested)
            {
                // Poll for key availability to avoid blocking forever
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var key = Console.ReadKey(intercept: true);
                if (key.Key != ConsoleKey.Escape) continue;

                // First ESC — show warning
                ChatRenderer.RenderWarning("Hit ESC again to cancel...");

                // Wait for second ESC within the window
                var deadline = DateTime.UtcNow + _doubleTapWindow;
                while (DateTime.UtcNow < deadline && !_turnCts.IsCancellationRequested)
                {
                    if (!Console.KeyAvailable)
                    {
                        Thread.Sleep(50);
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

                // Timed out without second ESC — reset
                ChatRenderer.RenderInfo("  (cancel aborted)");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the turn finishes
        }
        catch (InvalidOperationException)
        {
            // Console not available (e.g., redirected mid-operation)
        }
    }

    public void Dispose()
    {
        // Signal the monitor to stop, then wait briefly for cleanup
        if (!_turnCts.IsCancellationRequested)
        {
            _turnCts.Cancel();
        }

        _monitorTask.Wait(TimeSpan.FromMilliseconds(200));
        _turnCts.Dispose();
    }
}
