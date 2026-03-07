using System.Diagnostics;

namespace JD.AI.Rendering;

/// <summary>
/// An animated braille-dot spinner that shows elapsed time while
/// waiting for the first token from the LLM. Runs on a background
/// timer and clears itself when stopped.
/// </summary>
internal sealed class TurnSpinner : IDisposable
{
    private static readonly string[] Frames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly Timer _timer;
    private int _frame;
    private int _lastLineLength;
    private volatile bool _stopped;

    public TurnSpinner()
    {
        _timer = new Timer(Tick, null, 0, 80);
    }

    private void Tick(object? state)
    {
        if (_stopped) return;

        var elapsed = _sw.Elapsed;
        var spinner = Frames[_frame % Frames.Length];
        _frame++;

        var line = $"\r{spinner} Thinking... ({FormatElapsed(elapsed)})";
        _lastLineLength = line.Length;

        try
        {
            Console.Write(line);
        }
        catch (ObjectDisposedException)
        {
            // Console torn down during shutdown
        }
    }

    /// <summary>Stop the spinner and clear its output line.</summary>
    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        _sw.Stop();
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        // Clear the spinner line
        try
        {
            var clearLen = Math.Max(_lastLineLength, 40);
            Console.Write("\r" + new string(' ', clearLen) + "\r");
        }
        catch (ObjectDisposedException)
        {
            // Console torn down
        }
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }

    internal static string FormatElapsed(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{ts.Minutes}m {ts.Seconds:D2}s"
            : $"{ts.TotalSeconds:F1}s";
}
