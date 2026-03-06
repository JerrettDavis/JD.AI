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
    /// Accepts optional preamble lines (e.g. <c>diff --git</c>, <c>index …</c>)
    /// before the <c>---</c> / <c>+++</c> markers so that full <c>git diff</c>
    /// output is detected correctly.
    /// </summary>
    public static bool IsDiff(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lines = text.Split('\n');
        var minusFound = false;
        foreach (var rawLine in lines)
        {
            // TrimEnd only — preserve leading characters so "---"/"+++" must be at column 0
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (!minusFound)
            {
                if (line.StartsWith("---", StringComparison.Ordinal))
                    minusFound = true;
                // else: skip preamble lines (diff --git, index …) and keep looking
                continue;
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
