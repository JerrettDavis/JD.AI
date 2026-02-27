namespace JD.AI.Tui.Rendering;

/// <summary>
/// Interactive readline replacement with ghost-text completions, dropdown menu,
/// command syntax highlighting, and input history. Replaces Console.ReadLine()
/// for a Claude Code-like editing experience.
/// </summary>
public sealed class InteractiveInput
{
    private readonly CompletionProvider _completions;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    private const int PromptWidth = 2; // "> " prefix
    private const int MaxDropdownItems = 8;

    public InteractiveInput(CompletionProvider completions)
    {
        _completions = completions;
    }

    /// <summary>
    /// Reads a line of input with interactive completions.
    /// Returns null on Ctrl+C or when input is cancelled.
    /// Falls back to Console.ReadLine() when stdin is redirected.
    /// </summary>
    public string? ReadLine()
    {
        // Fall back to plain input when not interactive
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var buffer = new List<char>();
        var cursor = 0;
        IReadOnlyList<CompletionItem> matches = [];
        var selected = 0;
        var dropdownLines = 0;
        var inputRow = Console.CursorTop;

        RedrawAll();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearDropdown();
                    RedrawInputLine(showGhost: false);
                    Console.WriteLine();
                    var result = Str();
                    if (!string.IsNullOrWhiteSpace(result))
                        _history.Add(result);
                    _historyIndex = -1;
                    return result;

                case ConsoleKey.C when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    ClearDropdown();
                    Console.WriteLine();
                    return null;

                case ConsoleKey.Escape:
                    DismissCompletions();
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
                        cursor++;
                        SetCursorPos();
                    }
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursor > 0)
                    {
                        cursor--;
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
                        buffer.RemoveAt(cursor - 1);
                        cursor--;
                        RefreshCompletions();
                        RedrawAll();
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursor < buffer.Count)
                    {
                        buffer.RemoveAt(cursor);
                        RefreshCompletions();
                        RedrawAll();
                    }
                    break;

                default:
                    if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                    {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        RefreshCompletions();
                        RedrawAll();
                    }
                    break;
            }
        }

        // ── Local helpers ──────────────────────────────────────

        string Str() => new(buffer.ToArray());

        void AcceptCompletion()
        {
            ClearDropdown();
            buffer.Clear();
            buffer.AddRange(matches[selected].Text);
            buffer.Add(' ');
            cursor = buffer.Count;
            matches = [];
            selected = 0;
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

        void RedrawAll()
        {
            ClearDropdown();
            RedrawInputLine(showGhost: true);
            RenderDropdown();
            SetCursorPos();
        }

        void RedrawInputLine(bool showGhost)
        {
            Console.SetCursorPosition(PromptWidth, inputRow);

            // Clear from prompt to end of visible line
            var clearLen = Math.Max(0, Console.WindowWidth - PromptWidth - 1);
            Console.Write(new string(' ', clearLen));
            Console.SetCursorPosition(PromptWidth, inputRow);

            var text = Str();

            // Syntax highlight: slash commands in cyan
            if (text.StartsWith('/'))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(text);
                Console.ResetColor();
            }
            else
            {
                Console.Write(text);
            }

            // Ghost text for top completion
            if (showGhost && matches.Count > 0)
            {
                var completion = matches[selected].Text;
                if (completion.Length > text.Length)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(completion[text.Length..]);
                    Console.ResetColor();
                }
            }
        }

        void RenderDropdown()
        {
            // Only show dropdown when there are 2+ matches
            if (matches.Count < 2)
            {
                dropdownLines = 0;
                return;
            }

            var itemCount = Math.Min(matches.Count, MaxDropdownItems);

            // Ensure we have room below the input line.
            // If near the bottom of the buffer, force scroll by writing newlines.
            var windowHeight = Console.WindowHeight;
            var available = windowHeight - 1 - inputRow;
            if (available < itemCount)
            {
                var scrollNeeded = itemCount - available;
                Console.SetCursorPosition(0, windowHeight - 1);
                for (var i = 0; i < scrollNeeded; i++)
                    Console.WriteLine();
                inputRow -= scrollNeeded;
            }

            for (var i = 0; i < itemCount; i++)
            {
                var row = inputRow + 1 + i;
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

            for (var i = 0; i < dropdownLines; i++)
            {
                var row = inputRow + 1 + i;
                if (row < Console.WindowHeight)
                {
                    Console.SetCursorPosition(0, row);
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                }
            }

            dropdownLines = 0;
        }

        void SetCursorPos() =>
            Console.SetCursorPosition(PromptWidth + cursor, inputRow);
    }
}
