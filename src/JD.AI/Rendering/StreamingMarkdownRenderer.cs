using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Line-buffered progressive markdown renderer for streaming output.
/// Tokens appear as they arrive with basic markdown formatting.
/// Thread-safety: callers must synchronize externally (ChatRenderer uses _streamLock).
/// </summary>
internal sealed partial class StreamingMarkdownRenderer
{
    private readonly StringBuilder _lineBuffer = new();
    private bool _inCodeBlock;
    private string? _codeBlockLang;

    /// <summary>
    /// Process a chunk of streaming text. Renders complete lines immediately,
    /// buffers partial lines until a newline arrives.
    /// </summary>
    public void ProcessChunk(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                RenderLine(_lineBuffer.ToString());
                _lineBuffer.Clear();
            }
            else
            {
                _lineBuffer.Append(ch);
            }
        }
    }

    /// <summary>Render any remaining partial line in the buffer.</summary>
    public void Flush()
    {
        if (_lineBuffer.Length > 0)
        {
            RenderLine(_lineBuffer.ToString());
            _lineBuffer.Clear();
        }

        // Reset code block state in case stream ended mid-block
        _inCodeBlock = false;
        _codeBlockLang = null;
    }

    private void RenderLine(string line)
    {
        // Code block toggle
        if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            if (!_inCodeBlock)
            {
                _inCodeBlock = true;
                _codeBlockLang = line.TrimStart()[3..].Trim();
                var langLabel = string.IsNullOrEmpty(_codeBlockLang) ? "code" : _codeBlockLang;
                try
                {
                    AnsiConsole.MarkupLine($"  [dim]┌─ {Markup.Escape(langLabel)} ──[/]");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine($"  +- {langLabel} --");
                }
            }
            else
            {
                _inCodeBlock = false;
                _codeBlockLang = null;
                try
                {
                    AnsiConsole.MarkupLine("  [dim]└────[/]");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("  +----");
                }
            }
            return;
        }

        if (_inCodeBlock)
        {
            try
            {
                AnsiConsole.MarkupLine($"  [dim]│[/] [dim]{Markup.Escape(line)}[/]");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine($"  | {line}");
            }
            return;
        }

        // Empty line — pass through
        if (string.IsNullOrWhiteSpace(line))
        {
            Console.WriteLine();
            return;
        }

        var trimmed = line.TrimStart();

        // Headings
        if (trimmed.StartsWith("### ", StringComparison.Ordinal))
        {
            WriteMarkup($"[bold blue]{Markup.Escape(trimmed[4..])}[/]");
            return;
        }
        if (trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            WriteMarkup($"[bold cyan]{Markup.Escape(trimmed[3..])}[/]");
            return;
        }
        if (trimmed.StartsWith("# ", StringComparison.Ordinal))
        {
            WriteMarkup($"[bold yellow]{Markup.Escape(trimmed[2..])}[/]");
            return;
        }

        // Bullet lists
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            var indent = line.Length - trimmed.Length;
            var depth = indent / 2;
            var bullet = depth switch { 0 => "\u25cf", 1 => "\u25cb", _ => "\u25b8" }; // ●○▸
            var content = trimmed[2..];
            var pad = new string(' ', indent);
            WriteMarkup($"{pad}{bullet} {FormatInlineMarkup(content)}");
            return;
        }

        // Numbered lists
        if (NumberedListRegex().IsMatch(trimmed))
        {
            var match = NumberedListRegex().Match(trimmed);
            var num = match.Groups[1].Value;
            var content = match.Groups[2].Value;
            var indent = line.Length - trimmed.Length;
            var pad = new string(' ', indent);
            WriteMarkup($"{pad}[dim]{num}.[/] {FormatInlineMarkup(content)}");
            return;
        }

        // Regular text with inline formatting
        WriteMarkup(FormatInlineMarkup(line));
    }

    private static void WriteMarkup(string markup)
    {
        try
        {
            AnsiConsole.MarkupLine(markup);
        }
        catch (InvalidOperationException)
        {
            // AnsiConsole not available (e.g. redirected output)
            Console.WriteLine(Markup.Remove(markup));
        }
    }

    /// <summary>
    /// Apply inline markdown formatting (bold, italic, inline code) to a line.
    /// Pure function suitable for unit testing.
    /// </summary>
    internal static string FormatInlineMarkup(string line)
    {
        var escaped = Markup.Escape(line);

        // Inline code: `code` → [bold yellow on grey15]code[/]
        escaped = InlineCodeRegex().Replace(escaped, "[bold yellow on grey15]$1[/]");

        // Bold: **text** → [bold]text[/]
        escaped = BoldRegex().Replace(escaped, "[bold]$1[/]");

        // Italic: *text* → [italic]text[/]  (but not **text**)
        escaped = ItalicRegex().Replace(escaped, "[italic]$1[/]");

        return escaped;
    }

    [GeneratedRegex(@"^(\d+)\.\s+(.*)$")]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldRegex();

    // Match *text* but not **text** — lookbehind/lookahead to exclude double asterisks
    [GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")]
    private static partial Regex ItalicRegex();
}
