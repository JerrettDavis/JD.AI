using System.Diagnostics;
using JD.AI.Core.Config;

namespace JD.AI.Rendering;

/// <summary>
/// Style-aware progress indicator that replaces <see cref="TurnSpinner"/>.
/// Renders differently based on <see cref="SpinnerStyle"/>: from no output
/// to a full nerdy dashboard with throughput, time-to-first-token, and model info.
/// </summary>
internal sealed class TurnProgress : IDisposable
{
    private static readonly string[] BrailleFrames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly SpinnerStyle _style;
    private readonly string? _modelName;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly Timer _timer;
    private int _frame;
    private volatile bool _stopped;

    /// <summary>Elapsed milliseconds when the spinner was stopped (first content arrived).</summary>
    public long TimeToFirstTokenMs { get; private set; } = -1;

    public TurnProgress(SpinnerStyle style, string? modelName = null)
    {
        _style = style;
        _modelName = modelName;

        if (style == SpinnerStyle.None)
        {
            _timer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            return;
        }

        var interval = style == SpinnerStyle.Minimal ? 400 : 80;
        _timer = new Timer(Tick, null, 0, interval);
    }

    private void Tick(object? state)
    {
        if (_stopped) return;

        var elapsed = _sw.Elapsed;
        var line = _style switch
        {
            SpinnerStyle.Minimal => FormatMinimal(elapsed),
            SpinnerStyle.Normal => FormatNormal(elapsed),
            SpinnerStyle.Rich => FormatRich(elapsed),
            SpinnerStyle.Nerdy => FormatNerdy(elapsed),
            _ => string.Empty,
        };

        try
        {
            // Clear the line and write new content
            Console.Write($"\x1b[2K\r{line}");
        }
        catch (ObjectDisposedException)
        {
            // Console torn down during shutdown
        }
    }

    /// <summary>Stop the progress indicator and clear the line.</summary>
    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        TimeToFirstTokenMs = _sw.ElapsedMilliseconds;
        _sw.Stop();
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        try
        {
            Console.Write("\x1b[2K\r");
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

    // ── Style formatters ──────────────────────────────────

    private string FormatMinimal(TimeSpan elapsed)
    {
        // Alternating dot: subtle, minimal
        var dot = _frame++ % 2 == 0 ? "·" : " ";
        return $"  {dot} {FormatElapsed(elapsed)}";
    }

    private string FormatNormal(TimeSpan elapsed)
    {
        var spinner = BrailleFrames[_frame++ % BrailleFrames.Length];
        return $"  \x1b[36m{spinner}\x1b[0m Thinking... \x1b[2m{FormatElapsed(elapsed)}\x1b[0m";
    }

    private string FormatRich(TimeSpan elapsed)
    {
        var spinner = BrailleFrames[_frame++ % BrailleFrames.Length];
        var bar = BuildProgressBar(elapsed);
        return $"  \x1b[36m{spinner}\x1b[0m Thinking \x1b[2m{bar}\x1b[0m " +
               $"\x1b[2m{FormatElapsed(elapsed)}\x1b[0m";
    }

    private string FormatNerdy(TimeSpan elapsed)
    {
        var spinner = BrailleFrames[_frame++ % BrailleFrames.Length];
        var bar = BuildProgressBar(elapsed);
        var model = !string.IsNullOrEmpty(_modelName)
            ? $" │ \x1b[33m{_modelName}\x1b[0m"
            : "";
        return $"  \x1b[36m{spinner}\x1b[0m Thinking \x1b[2m{bar}\x1b[0m " +
               $"\x1b[2m{FormatElapsed(elapsed)}{model} │ awaiting first token\x1b[0m";
    }

    private static string BuildProgressBar(TimeSpan elapsed)
    {
        // Indeterminate progress: bouncing highlight across 10 chars
        const int width = 10;
        var pos = (int)(elapsed.TotalMilliseconds / 150) % (width * 2);
        if (pos >= width) pos = width * 2 - pos - 1;

        var chars = new char[width];
        for (var i = 0; i < width; i++)
            chars[i] = i == pos ? '━' : '░';

        return new string(chars);
    }

    private static string FormatElapsed(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{ts.Minutes}m {ts.Seconds:D2}s"
            : $"{ts.TotalSeconds:F1}s";
}
