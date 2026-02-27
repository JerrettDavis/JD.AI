using System.Diagnostics;

namespace JD.AI.Tui.Rendering;

/// <summary>
/// Interactive readline replacement with ghost-text completions, dropdown menu,
/// command syntax highlighting, clipboard paste detection, and input history.
/// Replaces Console.ReadLine() for a Claude Code-like editing experience.
/// </summary>
public sealed class InteractiveInput
{
    private readonly CompletionProvider _completions;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private DateTime _lastEscapeTime = DateTime.MinValue;

    private const int PromptWidth = 2; // "> " prefix
    private const int MaxDropdownItems = 8;
    private const int PasteBurstThresholdMs = 8;
    private const int PasteCollapseMinChars = 10;
    private static readonly TimeSpan EscapeDoubleWindow = TimeSpan.FromMilliseconds(1500);

    /// <summary>Fires when the user double-taps ESC at an empty prompt.</summary>
    public event EventHandler? OnDoubleEscape;

    public InteractiveInput(CompletionProvider completions)
    {
        _completions = completions;
    }

    /// <summary>
    /// Reads a line of input with interactive completions and paste support.
    /// Returns null on Ctrl+C or when input is cancelled.
    /// Falls back to Console.ReadLine() when stdin is redirected.
    /// </summary>
    public InputResult? ReadLineWithAttachments()
    {
        // Fall back to plain input when not interactive
        if (Console.IsInputRedirected)
        {
            var line = Console.ReadLine();
            return line is null ? null : new InputResult { TypedText = line };
        }

        var buffer = new List<char>();
        var cursor = 0;
        IReadOnlyList<CompletionItem> matches = [];
        var selected = 0;
        var dropdownLines = 0;
        var inputRow = Console.CursorTop;
        var inputLineCount = 1;
        var attachments = new List<PastedContent>();
        // Track chip positions: each chip occupies a range in the buffer
        var chipRanges = new List<(int Start, int Length, PastedContent Paste)>();

        RedrawAll();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearDropdown();
                    RedrawInputLine(showGhost: false);
                    SetCursorToEnd();
                    Console.WriteLine();
                    var typedText = ExtractTypedText();
                    if (!string.IsNullOrWhiteSpace(typedText) || attachments.Count > 0)
                        _history.Add(BuildDisplayText());
                    _historyIndex = -1;
                    return new InputResult { TypedText = typedText, Attachments = attachments.AsReadOnly() };

                case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    ClearDropdown();
                    Console.WriteLine();
                    return null;

                case ConsoleKey.V when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    HandleClipboardPaste();
                    break;

                case ConsoleKey.Escape:
                    if (matches.Count > 0)
                    {
                        DismissCompletions();
                    }
                    else if (buffer.Count == 0 && attachments.Count == 0)
                    {
                        var now = DateTime.UtcNow;
                        if (now - _lastEscapeTime <= EscapeDoubleWindow)
                        {
                            _lastEscapeTime = DateTime.MinValue;
                            OnDoubleEscape?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            _lastEscapeTime = now;
                        }
                    }
                    else
                    {
                        DismissCompletions();
                    }
                    break;

                case ConsoleKey.Tab:
                    if (matches.Count > 0)
                        AcceptCompletion();
                    break;

                case ConsoleKey.UpArrow:
                    if (matches.Count > 0)
                    {
                        selected = (selected - 1 + matches.Count) % matches.Count;
                        RedrawAll();
                    }
                    else
                    {
                        NavigateHistory(-1);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (matches.Count > 0)
                    {
                        selected = (selected + 1) % matches.Count;
                        RedrawAll();
                    }
                    else
                    {
                        NavigateHistory(1);
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (matches.Count > 0 && cursor == buffer.Count)
                    {
                        AcceptCompletion();
                    }
                    else if (cursor < buffer.Count)
                    {
                        // Skip over chip if cursor is at chip start
                        var chip = FindChipAt(cursor);
                        cursor = chip is not null ? chip.Value.Start + chip.Value.Length : cursor + 1;
                        SetCursorPos();
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursor > 0)
                    {
                        // Skip over chip if cursor is at chip end
                        var chip = FindChipEndingAt(cursor);
                        cursor = chip is not null ? chip.Value.Start : cursor - 1;
                        SetCursorPos();
                    }
                    break;

                case ConsoleKey.Home:
                    cursor = 0;
                    SetCursorPos();
                    break;

                case ConsoleKey.End:
                    cursor = buffer.Count;
                    SetCursorPos();
                    break;

                case ConsoleKey.Backspace:
                    if (cursor > 0)
                    {
                        // Check if we're backspacing into a chip
                        var chip = FindChipEndingAt(cursor);
                        if (chip is not null)
                        {
                            RemoveChip(chip.Value);
                        }
                        else
                        {
                            buffer.RemoveAt(cursor - 1);
                            cursor--;
                        }
                        RefreshCompletions();
                        RedrawAll();
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Count)
                    {
                        var chip = FindChipAt(cursor);
                        if (chip is not null)
                        {
                            RemoveChip(chip.Value);
                        }
                        else
                        {
                            buffer.RemoveAt(cursor);
                        }
                        RefreshCompletions();
                        RedrawAll();
                    }
                    break;

                default:
                    if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                    {
                        // Burst detection: read ahead rapidly to detect paste
                        var burstChars = DetectBurst(key.KeyChar);
                        if (burstChars is not null)
                        {
                            InsertPastedContent(burstChars);
                        }
                        else
                        {
                            buffer.Insert(cursor, key.KeyChar);
                            cursor++;
                        }
                        RefreshCompletions();
                        RedrawAll();
                    }
                    break;
            }
        }

        // ── Local helpers ──────────────────────────────────────

        string Str() => new(buffer.ToArray());

        // Extract just the user-typed text (without chip placeholder chars)
        string ExtractTypedText()
        {
            var chars = new List<char>();
            for (var i = 0; i < buffer.Count; i++)
            {
                if (chipRanges.Any(c => i >= c.Start && i < c.Start + c.Length))
                    continue;
                chars.Add(buffer[i]);
            }
            return new string(chars.ToArray()).Trim();
        }

        string BuildDisplayText() => Str();

        int WindowWidth() => Math.Max(1, Console.WindowWidth);

        int WrapCol(int charOffset) => (PromptWidth + charOffset) % WindowWidth();
        int WrapRow(int charOffset) => inputRow + (PromptWidth + charOffset) / WindowWidth();

        int CalcInputLines(int totalChars) =>
            Math.Max(1, (PromptWidth + totalChars - 1) / WindowWidth() + 1);

        // ── Paste detection ────────────────────────────────────

        string? DetectBurst(char firstChar)
        {
            // Try to read a burst of characters arriving very fast (paste)
            var sw = Stopwatch.StartNew();
            var collected = new List<char> { firstChar };

            while (Console.KeyAvailable && sw.ElapsedMilliseconds < 200)
            {
                var next = Console.ReadKey(intercept: true);
                collected.Add(next.KeyChar != '\0' ? next.KeyChar : ' ');

                // Check if chars are still arriving rapidly
                if (!Console.KeyAvailable)
                {
                    // Wait a tiny bit to see if more are coming
                    Thread.Sleep(PasteBurstThresholdMs);
                }
            }

            // If we only got 1 char, it's not a paste
            if (collected.Count <= 1)
                return null;

            var text = new string(collected.ToArray());
            var lineCount = text.Split('\n').Length;

            // Only collapse if it meets the threshold
            if (text.Length < PasteCollapseMinChars && lineCount < 2)
                return null;

            return text;
        }

        void HandleClipboardPaste()
        {
            // Try reading from OS clipboard
            string? clipText = null;
            try
            {
                clipText = ReadClipboard();
            }
#pragma warning disable CA1031 // best-effort clipboard read
            catch { /* clipboard access may fail */ }
#pragma warning restore CA1031

            if (string.IsNullOrEmpty(clipText))
                return;

            InsertPastedContent(clipText);
            RefreshCompletions();
            RedrawAll();
        }

        void InsertPastedContent(string text)
        {
            var lineCount = text.Split('\n').Length;

            // Small single-line pastes → inline
            if (text.Length < PasteCollapseMinChars && lineCount < 2)
            {
                foreach (var ch in text)
                {
                    buffer.Insert(cursor, ch);
                    cursor++;
                }
                return;
            }

            // Collapse into a chip
            var paste = new PastedContent(text);
            attachments.Add(paste);

            var chipText = paste.Chip;
            var chipStart = cursor;

            foreach (var ch in chipText)
            {
                buffer.Insert(cursor, ch);
                cursor++;
            }

            chipRanges.Add((chipStart, chipText.Length, paste));
        }

        void RemoveChip((int Start, int Length, PastedContent Paste) chip)
        {
            // Remove chip characters from buffer
            for (var i = 0; i < chip.Length && chip.Start < buffer.Count; i++)
                buffer.RemoveAt(chip.Start);

            cursor = chip.Start;

            // Remove from attachments and chipRanges
            attachments.Remove(chip.Paste);
            chipRanges.Remove(chip);

            // Adjust subsequent chip ranges
            for (var i = 0; i < chipRanges.Count; i++)
            {
                var r = chipRanges[i];
                if (r.Start > chip.Start)
                    chipRanges[i] = (r.Start - chip.Length, r.Length, r.Paste);
            }
        }

        (int Start, int Length, PastedContent Paste)? FindChipAt(int pos)
        {
            foreach (var r in chipRanges)
            {
                if (pos >= r.Start && pos < r.Start + r.Length)
                    return r;
            }
            return null;
        }

        (int Start, int Length, PastedContent Paste)? FindChipEndingAt(int pos)
        {
            foreach (var r in chipRanges)
            {
                if (pos > r.Start && pos <= r.Start + r.Length)
                    return r;
            }
            return null;
        }

        // ── Completions & history ──────────────────────────────

        void AcceptCompletion()
        {
            ClearDropdown();
            buffer.Clear();
            buffer.AddRange(matches[selected].Text);
            buffer.Add(' ');
            cursor = buffer.Count;
            matches = [];
            selected = 0;
            chipRanges.Clear(); // completions replace everything
            RedrawInputLine(showGhost: false);
            SetCursorPos();
        }

        void DismissCompletions()
        {
            ClearDropdown();
            matches = [];
            selected = 0;
            RedrawInputLine(showGhost: false);
        }

        void NavigateHistory(int direction)
        {
            if (_history.Count == 0) return;

            ClearDropdown();

            if (_historyIndex == -1)
                _historyIndex = direction < 0 ? _history.Count : -1;

            _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);

            buffer.Clear();
            chipRanges.Clear();
            attachments.Clear();
            if (_historyIndex < _history.Count)
                buffer.AddRange(_history[_historyIndex]);

            cursor = buffer.Count;
            matches = [];
            selected = 0;
            RedrawInputLine(showGhost: false);
            SetCursorPos();
        }

        void RefreshCompletions()
        {
            matches = _completions.GetCompletions(Str());
            selected = 0;
        }

        // ── Rendering ──────────────────────────────────────────

        void RedrawAll()
        {
            ClearDropdown();
            RedrawInputLine(showGhost: true);
            RenderDropdown();
            SetCursorPos();
        }

        void RedrawInputLine(bool showGhost)
        {
            var w = WindowWidth();

            // Clear all rows the previous input occupied
            for (var row = 0; row < inputLineCount; row++)
            {
                var r = inputRow + row;
                if (r >= Console.BufferHeight) break;
                Console.SetCursorPosition(row == 0 ? PromptWidth : 0, r);
                var clearLen = row == 0 ? Math.Max(0, w - PromptWidth) : w;
                if (clearLen > 0)
                    Console.Write(new string(' ', clearLen));
            }

            Console.SetCursorPosition(PromptWidth, inputRow);

            // Render buffer with chip highlighting
            var pos = 0;
            while (pos < buffer.Count)
            {
                var chip = FindChipAt(pos);
                if (chip is not null)
                {
                    // Render chip with distinct styling
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.ForegroundColor = ConsoleColor.White;
                    for (var i = 0; i < chip.Value.Length && pos < buffer.Count; i++, pos++)
                        Console.Write(buffer[pos]);
                    Console.ResetColor();
                }
                else if (buffer[pos] == '/' && pos == 0)
                {
                    // Slash command highlighting
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    while (pos < buffer.Count && FindChipAt(pos) is null)
                    {
                        Console.Write(buffer[pos]);
                        pos++;
                    }
                    Console.ResetColor();
                }
                else
                {
                    Console.Write(buffer[pos]);
                    pos++;
                }
            }

            var totalChars = buffer.Count;

            // Ghost text for top completion
            if (showGhost && matches.Count > 0)
            {
                var text = Str();
                var completion = matches[selected].Text;
                if (completion.Length > text.Length)
                {
                    var ghost = completion[text.Length..];
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(ghost);
                    Console.ResetColor();
                    totalChars = PromptWidth + completion.Length - PromptWidth;
                    totalChars = completion.Length;
                }
            }

            inputLineCount = CalcInputLines(totalChars);

            // If wrapping pushed us near the bottom, adjust inputRow for scroll
            var lastRow = WrapRow(totalChars > 0 ? totalChars - 1 : 0);
            if (lastRow >= Console.BufferHeight)
            {
                var overflow = lastRow - Console.BufferHeight + 1;
                inputRow -= overflow;
            }
        }

        void RenderDropdown()
        {
            if (matches.Count < 2)
            {
                dropdownLines = 0;
                return;
            }

            var itemCount = Math.Min(matches.Count, MaxDropdownItems);
            var dropdownStart = inputRow + inputLineCount;

            var windowHeight = Console.WindowHeight;
            var available = windowHeight - dropdownStart;
            if (available < itemCount)
            {
                var scrollNeeded = itemCount - available;
                Console.SetCursorPosition(0, windowHeight - 1);
                for (var i = 0; i < scrollNeeded; i++)
                    Console.WriteLine();
                inputRow -= scrollNeeded;
                dropdownStart = inputRow + inputLineCount;
            }

            for (var i = 0; i < itemCount; i++)
            {
                var row = dropdownStart + i;
                if (row >= Console.BufferHeight) break;
                Console.SetCursorPosition(1, row);

                var item = matches[i];
                var marker = i == selected ? "▸" : " ";

                if (i == selected)
                {
                    Console.BackgroundColor = ConsoleColor.DarkBlue;
                    Console.ForegroundColor = ConsoleColor.White;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                var desc = item.Description != null ? $"  {item.Description}" : "";
                var label = $" {marker} {item.Text,-16}{desc}";
                var maxLen = Math.Min(label.Length, Console.WindowWidth - 3);
                Console.Write(label[..maxLen].PadRight(maxLen));
                Console.ResetColor();
            }

            dropdownLines = itemCount;
        }

        void ClearDropdown()
        {
            if (dropdownLines == 0) return;

            var dropdownStart = inputRow + inputLineCount;
            for (var i = 0; i < dropdownLines; i++)
            {
                var row = dropdownStart + i;
                if (row < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, row);
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                }
            }

            dropdownLines = 0;
        }

        void SetCursorPos()
        {
            var col = WrapCol(cursor);
            var row = WrapRow(cursor);
            col = Math.Clamp(col, 0, Math.Max(0, Console.BufferWidth - 1));
            row = Math.Clamp(row, 0, Math.Max(0, Console.BufferHeight - 1));
            Console.SetCursorPosition(col, row);
        }

        void SetCursorToEnd()
        {
            var endOffset = buffer.Count;
            var col = WrapCol(endOffset);
            var row = WrapRow(endOffset);
            col = Math.Clamp(col, 0, Math.Max(0, Console.BufferWidth - 1));
            row = Math.Clamp(row, 0, Math.Max(0, Console.BufferHeight - 1));
            Console.SetCursorPosition(col, row);
        }
    }

    /// <summary>Legacy wrapper that returns a plain string.</summary>
    public string? ReadLine()
    {
        var result = ReadLineWithAttachments();
        return result?.AssemblePrompt();
    }

    /// <summary>
    /// Reads text from the OS clipboard. Cross-platform: uses powershell on Windows,
    /// pbpaste on macOS, xclip/xsel on Linux.
    /// </summary>
    private static string? ReadClipboard()
    {
        string fileName;
        string arguments;

        if (OperatingSystem.IsWindows())
        {
            fileName = "powershell";
            arguments = "-NoProfile -Command Get-Clipboard";
        }
        else if (OperatingSystem.IsMacOS())
        {
            fileName = "pbpaste";
            arguments = "";
        }
        else
        {
            // Try xclip first, fall back to xsel
            fileName = "xclip";
            arguments = "-selection clipboard -o";
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return null;

        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(3000);

        return string.IsNullOrEmpty(output) ? null : output;
    }
}
