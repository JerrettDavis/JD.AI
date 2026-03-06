using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Renders unified diff output with red/green ANSI backgrounds —
/// the same visual style used by git diff and similar tools.
/// No markdown processing is applied; diffs are output verbatim with color.
/// </summary>
public static class DiffRenderer
{
    /// <summary>
    /// Returns true when <paramref name="text"/> looks like a unified diff.
    /// Matches when the first two non-empty lines start with "---" and "+++".
    /// </summary>
    public static bool IsDiff(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lines = text.Split('\n');
        var minusFound = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart().TrimEnd('\r');
            if (line.Length == 0) continue;
            if (!minusFound)
            {
                if (line.StartsWith("---", StringComparison.Ordinal)) { minusFound = true; continue; }
                return false;
            }
            return line.StartsWith("+++", StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// Render a unified diff with colored line prefixes.
    /// Added lines (+) → green; removed lines (−) → red; hunk headers (@@) → cyan; context → default.
    /// </summary>
    public static void Render(string diff)
    {
        AnsiConsole.WriteLine();
        foreach (var rawLine in diff.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("+++", StringComparison.Ordinal) ||
                line.StartsWith("---", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[bold]{Markup.Escape(line)}[/]");
            }
            else if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine($"[cyan]{Markup.Escape(line)}[/]");
            }
            else if (line.StartsWith('+'))
            {
                // Green background for additions
                AnsiConsole.MarkupLine($"[bold green]{Markup.Escape(line)}[/]");
            }
            else if (line.StartsWith('-'))
            {
                // Red for removals
                AnsiConsole.MarkupLine($"[bold red]{Markup.Escape(line)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]{Markup.Escape(line)}[/]");
            }
        }
        AnsiConsole.WriteLine();
    }
}
