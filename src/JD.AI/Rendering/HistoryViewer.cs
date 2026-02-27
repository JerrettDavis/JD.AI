using JD.AI.Tui.Persistence;

namespace JD.AI.Tui.Rendering;

/// <summary>
/// Interactive session history browser with scrollable turns, details, and rollback.
/// Activated by double-tap ESC at an empty prompt.
/// </summary>
public static class HistoryViewer
{
    /// <summary>
    /// Show the history viewer and return the turn index to rollback to, or null to dismiss.
    /// </summary>
    public static int? Show(SessionInfo session)
    {
        if (session.Turns.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  No turns in this session.");
            Console.ResetColor();
            return null;
        }

        var selected = session.Turns.Count - 1;
        var showDetail = false;

        while (true)
        {
            Render(session, selected, showDetail);

            if (Console.IsInputRedirected)
                return null;

            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (selected > 0) selected--;
                    showDetail = false;
                    break;

                case ConsoleKey.DownArrow:
                    if (selected < session.Turns.Count - 1) selected++;
                    showDetail = false;
                    break;

                case ConsoleKey.Enter:
                    showDetail = !showDetail;
                    break;

                case ConsoleKey.R when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    // Ctrl+R = rollback to this turn
                    return selected;

                case ConsoleKey.Escape:
                case ConsoleKey.Q:
                    return null;
            }
        }
    }

    private static void Render(SessionInfo session, int selected, bool showDetail)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  📜 Session History — {session.Name ?? session.Id} ({session.Turns.Count} turns)");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  ↑↓ Navigate • Enter: Details • Ctrl+R: Rollback • Esc/Q: Close");
        Console.ResetColor();
        Console.WriteLine();

        var pageSize = Math.Max(Console.WindowHeight - 8, 5);
        var start = Math.Max(0, selected - pageSize / 2);
        var end = Math.Min(session.Turns.Count, start + pageSize);

        for (var i = start; i < end; i++)
        {
            var turn = session.Turns[i];
            var isSelected = i == selected;
            var prefix = isSelected ? " ▸ " : "   ";
            var role = string.Equals(turn.Role, "user", StringComparison.Ordinal) ? "👤" : "🤖";
            var preview = (turn.Content ?? "").Replace('\n', ' ');
            if (preview.Length > 60)
                preview = string.Concat(preview.AsSpan(0, 57), "...");

            if (isSelected)
                Console.ForegroundColor = ConsoleColor.White;
            else
                Console.ForegroundColor = ConsoleColor.Gray;

            var tools = turn.ToolCalls.Count > 0 ? $" [{turn.ToolCalls.Count}🔧]" : "";
            Console.WriteLine($"{prefix}{turn.TurnIndex}. {role} {preview}{tools}");
        }

        Console.ResetColor();

        if (showDetail)
        {
            Console.WriteLine();
            RenderDetail(session.Turns[selected]);
        }
    }

    private static void RenderDetail(TurnRecord turn)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ── Details ──────────────────────────");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  ID:        {turn.Id}");
        Console.WriteLine($"  Role:      {turn.Role}");
        Console.WriteLine($"  Model:     {turn.ModelId ?? "—"}");
        Console.WriteLine($"  Provider:  {turn.ProviderName ?? "—"}");
        Console.WriteLine($"  Tokens:    {turn.TokensIn} in / {turn.TokensOut} out");
        Console.WriteLine($"  Duration:  {turn.DurationMs}ms");
        Console.WriteLine($"  Time:      {turn.CreatedAt:g}");
        Console.ResetColor();

        if (turn.ToolCalls.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Tool Calls ({turn.ToolCalls.Count}):");
            foreach (var tc in turn.ToolCalls)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"    {tc.ToolName} ({tc.Status}, {tc.DurationMs}ms)");
            }
            Console.ResetColor();
        }

        if (turn.FilesTouched.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Files Touched ({turn.FilesTouched.Count}):");
            foreach (var ft in turn.FilesTouched)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"    {ft.Operation}: {ft.FilePath}");
            }
            Console.ResetColor();
        }

        if (!string.IsNullOrEmpty(turn.ThinkingText))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var think = turn.ThinkingText.Length > 200
                ? string.Concat(turn.ThinkingText.AsSpan(0, 197), "...")
                : turn.ThinkingText;
            Console.WriteLine($"  💭 {think}");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  Content: ");
        Console.ResetColor();
        var content = turn.Content ?? "(empty)";
        if (content.Length > 300)
            content = string.Concat(content.AsSpan(0, 297), "...");
        Console.WriteLine($"  {content}");
    }
}
