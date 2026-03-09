using System.Diagnostics;
using System.Text;
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
    private readonly System.Threading.Lock _renderLock = new();
    private int _frame;
    private int _renderedLineCount;
    private volatile bool _stopped;
    private volatile bool _paused;
    private volatile string? _thinkingPreview;
    private volatile int _thinkingTokenCount;

    /// <summary>Elapsed milliseconds when the spinner was stopped (first content arrived).</summary>
    public long TimeToFirstTokenMs { get; private set; } = -1;

    public TurnProgress(SpinnerStyle style, string? modelName = null)
    {
        _style = style;
        _modelName = modelName;

        // Suppress spinner entirely in JSON mode to prevent ANSI interleaving
        if (style == SpinnerStyle.None || ChatRenderer.CurrentOutputStyle == OutputStyle.Json)
        {
            _timer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            return;
        }

        var interval = style == SpinnerStyle.Minimal ? 400 : 80;
        _timer = new Timer(Tick, null, 0, interval);
    }

    private void Tick(object? state)
    {
        if (_stopped || _paused) return;

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
            lock (_renderLock)
            {
                ClearRenderedBlockNoLock();
                Console.Write(line);
                _renderedLineCount = CountRenderedLines(line);
            }
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
            lock (_renderLock)
            {
                ClearRenderedBlockNoLock();
            }
        }
        catch (ObjectDisposedException)
        {
            // Console torn down
        }
    }

    /// <summary>
    /// Temporarily pause the spinner so other output can be written cleanly.
    /// The spinner line is cleared but the timer is preserved for resumption.
    /// </summary>
    public void Pause()
    {
        if (_stopped || _paused) return;
        _paused = true;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        try
        {
            lock (_renderLock)
            {
                ClearRenderedBlockNoLock();
            }
        }
        catch (ObjectDisposedException)
        {
            // Console torn down
        }
    }

    /// <summary>Resume the spinner after a <see cref="Pause"/>.</summary>
    public void Resume()
    {
        if (_stopped || !_paused) return;
        _paused = false;

        var interval = _style == SpinnerStyle.Minimal ? 400 : 80;
        _timer.Change(0, interval);
    }

    /// <summary>Updates inline thinking preview text displayed next to the spinner.</summary>
    public void SetThinkingPreview(string? preview)
    {
        if (string.IsNullOrWhiteSpace(preview))
        {
            _thinkingPreview = null;
            return;
        }

        var normalized = preview
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        _thinkingPreview = lines.Count == 0
            ? null
            : string.Join('\n', lines);
    }

    /// <summary>Updates thinking token count shown in Nerdy mode.</summary>
    public void SetThinkingTokenCount(int tokens)
    {
        _thinkingTokenCount = Math.Max(0, tokens);
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }

    // ── Style formatters ──────────────────────────────────

    internal string FormatMinimal(TimeSpan elapsed)
    {
        // Alternating dot: subtle, minimal
        var dot = _frame++ % 2 == 0 ? "·" : " ";
        return $"  {dot} {FormatElapsed(elapsed)}";
    }

    internal string FormatNormal(TimeSpan elapsed)
    {
        var spinner = BrailleFrames[_frame++ % BrailleFrames.Length];
        var core = $"  \x1b[36m{spinner}\x1b[0m Thinking... \x1b[2m{FormatElapsed(elapsed)}\x1b[0m";
        var preview = GetLastPreviewLine();
        return AppendPreview(core, preview, 96, multiline: false);
    }

    internal string FormatRich(TimeSpan elapsed)
    {
        var spinner = BrailleFrames[_frame++ % BrailleFrames.Length];
        var bar = BuildProgressBar(elapsed);
        var core = $"  \x1b[36m{spinner}\x1b[0m Thinking \x1b[2m{bar}\x1b[0m " +
                   $"\x1b[2m{FormatElapsed(elapsed)}\x1b[0m";
        return AppendPreview(core, _thinkingPreview, 120, multiline: true);
    }

    internal string FormatNerdy(TimeSpan elapsed)
    {
        var spinner = BrailleFrames[_frame++ % BrailleFrames.Length];
        var bar = BuildProgressBar(elapsed);
        var model = !string.IsNullOrEmpty(_modelName)
            ? $" │ \x1b[33m{_modelName}\x1b[0m"
            : "";
        var thinkTokens = _thinkingTokenCount >= 0 ? _thinkingTokenCount : 0;
        var core = $"  \x1b[36m{spinner}\x1b[0m Thinking \x1b[2m{bar}\x1b[0m " +
                   $"\x1b[2m{FormatElapsed(elapsed)}{model} │ {thinkTokens} think-tok\x1b[0m";
        return AppendPreview(core, _thinkingPreview, 140, multiline: true);
    }

    internal static string BuildProgressBar(TimeSpan elapsed)
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

    internal static string FormatElapsed(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{ts.Minutes}m {ts.Seconds:D2}s"
            : $"{ts.TotalSeconds:F1}s";

    private void ClearRenderedBlockNoLock()
    {
        if (_renderedLineCount <= 0)
            return;

        for (var i = 0; i < _renderedLineCount - 1; i++)
        {
            Console.Write("\x1b[2K\r\x1b[1A");
        }

        Console.Write("\x1b[2K\r");
        _renderedLineCount = 0;
    }

    private static int CountRenderedLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var lines = 1;
        foreach (var c in text)
        {
            if (c == '\n')
                lines++;
        }

        return lines;
    }

    private string? GetLastPreviewLine()
    {
        if (string.IsNullOrWhiteSpace(_thinkingPreview))
            return null;

        var normalized = _thinkingPreview
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return lines.Length == 0 ? null : lines[^1];
    }

    private static string AppendPreview(string core, string? preview, int maxChars, bool multiline)
    {
        if (string.IsNullOrWhiteSpace(preview))
            return core;

        var normalized = preview
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (lines.Count == 0)
            return core;

        if (!multiline || lines.Count == 1)
        {
            var compact = lines[0];
            if (compact.Length > maxChars)
                compact = string.Concat(compact.AsSpan(0, maxChars - 3), "...");

            return $"{core} \x1b[2m│ {compact}\x1b[0m";
        }

        var sb = new StringBuilder(core);
        foreach (var line in lines.Take(4))
        {
            var compact = line;
            if (compact.Length > maxChars)
                compact = string.Concat(compact.AsSpan(0, maxChars - 3), "...");
            sb.Append('\n').Append("  \x1b[2m").Append(compact).Append("\x1b[0m");
        }

        return sb.ToString();
    }
}
