using System.Text;
using JD.AI.Commands;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Renders markdown text using Spectre.Console primitives.
/// Parses with Markdig and walks the AST to produce styled terminal output.
///
/// Highlights:
/// - Headings (H1→bold yellow, H2→bold cyan, H3→bold blue, H4+→bold)
/// - Bold / italic / inline code
/// - Fenced code blocks with simple keyword highlighting and a panel border
/// - Ordered and unordered lists (nested, with ●/○/▸ bullets)
/// - Tables via Spectre.Console Table
/// - Block quotes with │ gutter
/// - Thematic breaks (─── rule)
/// - Slash-command tokens in prose are colored when they match registered commands
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // All known slash-command prefixes (e.g. "/help", "/model", …)
    private static readonly HashSet<string> KnownSlashCommands = new(
        SlashCommandCatalog.CompletionEntries
            .Select(e => e.Command.Split(' ')[0])
            .Distinct(),
        StringComparer.OrdinalIgnoreCase);

    // Bullets for list nesting depth 0-2+
    private static readonly string[] Bullets = ["●", "○", "▸"];

    // ── Public entry points ─────────────────────────────────

    /// <summary>
    /// Render <paramref name="markdown"/> to the current console.
    /// If the text is a unified diff, delegates to <see cref="DiffRenderer"/>.
    /// </summary>
    public static void Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            AnsiConsole.WriteLine();
            return;
        }

        if (DiffRenderer.IsDiff(markdown))
        {
            DiffRenderer.Render(markdown);
            return;
        }

        var doc = Markdown.Parse(markdown, Pipeline);
        foreach (var block in doc)
            RenderBlock(block, 0);
    }

    // ── Block renderers ─────────────────────────────────────

    private static void RenderBlock(Block block, int depth)
    {
        switch (block)
        {
            case HeadingBlock h: RenderHeading(h); break;
            case FencedCodeBlock fc: RenderFencedCode(fc); break;
            case CodeBlock cb: RenderCodeBlock(cb); break;
            case ParagraphBlock p: RenderParagraph(p); break;
            case ListBlock lb: RenderList(lb, depth); break;
            case QuoteBlock qb: RenderQuote(qb); break;
            case ThematicBreakBlock: AnsiConsole.Write(new Rule()); AnsiConsole.WriteLine(); break;
            case Markdig.Extensions.Tables.Table t: RenderTable(t); break;
            case HtmlBlock: break; // skip raw HTML
            case ContainerBlock cb2:
                foreach (var child in cb2) RenderBlock(child, depth);
                break;
            default: break;
        }
    }

    private static void RenderHeading(HeadingBlock h)
    {
        AnsiConsole.WriteLine();
        var text = GetInlineText(h.Inline);
        var markup = h.Level switch
        {
            1 => $"[bold yellow]{Markup.Escape(text)}[/]",
            2 => $"[bold cyan]{Markup.Escape(text)}[/]",
            3 => $"[bold blue]{Markup.Escape(text)}[/]",
            4 => $"[bold green]{Markup.Escape(text)}[/]",
            _ => $"[bold]{Markup.Escape(text)}[/]",
        };
        AnsiConsole.MarkupLine(markup);
        AnsiConsole.WriteLine();
    }

    private static void RenderParagraph(ParagraphBlock p)
    {
        if (p.Inline is null) return;
        var markup = RenderInlines(p.Inline, highlightSlashCommands: true);
        AnsiConsole.MarkupLine(markup);
        AnsiConsole.WriteLine();
    }

    private static void RenderFencedCode(FencedCodeBlock fc)
    {
        var lang = fc.Info?.Trim() ?? string.Empty;
        RenderCodeContent(fc.Lines.ToString(), lang);
    }

    private static void RenderCodeBlock(CodeBlock cb)
    {
        RenderCodeContent(cb.Lines.ToString(), string.Empty);
    }

    private static void RenderCodeContent(string rawCode, string lang)
    {
        var lines = rawCode.TrimEnd('\n', '\r').Split('\n');
        var sb = new StringBuilder();
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            sb.AppendLine(HighlightCodeLine(line, lang));
        }

        var header = string.IsNullOrEmpty(lang) ? "code" : lang;
        var panel = new Panel(new Markup(sb.ToString().TrimEnd()))
            .Header($"[dim]{Markup.Escape(header)}[/]", Justify.Left)
            .BorderColor(Color.Grey23)
            .Padding(1, 0)
            .Expand();

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static void RenderList(ListBlock lb, int depth)
    {
        var bullet = Bullets[Math.Min(depth, Bullets.Length - 1)];
        var num = 1;

        foreach (var item in lb.OfType<ListItemBlock>())
        {
            foreach (var child in item)
            {
                if (child is ParagraphBlock p && p.Inline is not null)
                {
                    var text = RenderInlines(p.Inline, highlightSlashCommands: true);
                    var prefix = lb.IsOrdered
                        ? $"[dim]{num++}.[/]"
                        : $"[cyan]{Markup.Escape(bullet)}[/]";
                    var indent = new string(' ', depth * 2);
                    AnsiConsole.MarkupLine($"{indent}  {prefix} {text}");
                }
                else if (child is ListBlock nested)
                {
                    RenderList(nested, depth + 1);
                }
                else
                {
                    RenderBlock(child, depth + 1);
                }
            }
        }

        if (depth == 0)
            AnsiConsole.WriteLine();
    }

    private static void RenderQuote(QuoteBlock qb)
    {
        // Capture each block's text output so we can prefix every line with the gutter
        var sw = new StringWriter();
        var saved = Console.Out;
        Console.SetOut(sw);
        try
        {
            foreach (var block in qb)
                RenderBlock(block, 0);
        }
        finally
        {
            Console.SetOut(saved);
        }

        foreach (var line in sw.ToString().Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            AnsiConsole.MarkupLine($"[dim grey]│[/] [italic]{Markup.Escape(trimmed)}[/]");
        }
    }

    private static void RenderTable(Markdig.Extensions.Tables.Table table)
    {
        var spectreTable = new Spectre.Console.Table()
            .BorderColor(Color.Grey)
            .Border(TableBorder.Simple);

        var rows = table.OfType<Markdig.Extensions.Tables.TableRow>().ToList();
        if (rows.Count == 0) return;

        // Header row
        foreach (var cell in rows[0].OfType<Markdig.Extensions.Tables.TableCell>())
            spectreTable.AddColumn(new TableColumn(
                new Markup(RenderInlines(cell.OfType<ParagraphBlock>()
                    .SelectMany(p => p.Inline ?? Enumerable.Empty<Inline>())))));

        // Body rows — clamp to column count to prevent Spectre from throwing,
        // and pad short rows so every row has the same width.
        var columnCount = spectreTable.Columns.Count;
        foreach (var row in rows.Skip(1))
        {
            var cells = row.OfType<Markdig.Extensions.Tables.TableCell>()
                .Take(columnCount)
                .Select(c => new Markup(RenderInlines(
                    c.OfType<ParagraphBlock>().SelectMany(p => p.Inline ?? Enumerable.Empty<Inline>()))))
                .Cast<Spectre.Console.Rendering.IRenderable>()
                .ToList();

            // Pad missing cells so the row width always matches the header
            while (cells.Count < columnCount)
                cells.Add(new Markup(string.Empty));

            spectreTable.AddRow(cells.ToArray());
        }

        AnsiConsole.Write(spectreTable);
        AnsiConsole.WriteLine();
    }

    // ── Inline rendering ────────────────────────────────────

    /// <summary>Render a ContainerInline to a Spectre markup string.</summary>
    private static string RenderInlines(ContainerInline? container, bool highlightSlashCommands = false)
    {
        if (container is null) return string.Empty;
        return RenderInlines(container.AsEnumerable(), highlightSlashCommands);
    }

    private static string RenderInlines(IEnumerable<Inline> inlines, bool highlightSlashCommands = false)
    {
        var sb = new StringBuilder();
        foreach (var inline in inlines)
            sb.Append(RenderInline(inline, highlightSlashCommands));
        return sb.ToString();
    }

    private static string RenderInline(Inline inline, bool highlightSlashCommands)
    {
        return inline switch
        {
            LiteralInline lit =>
                highlightSlashCommands
                    ? ColorizeSlashCommands(Markup.Escape(lit.Content.ToString()))
                    : Markup.Escape(lit.Content.ToString()),

            EmphasisInline em when em.DelimiterCount == 2 =>
                $"[bold]{RenderInlines(em, highlightSlashCommands)}[/]",

            EmphasisInline em =>
                $"[italic]{RenderInlines(em, highlightSlashCommands)}[/]",

            CodeInline code =>
                $"[bold yellow on grey15]{Markup.Escape(code.Content)}[/]",

            LinkInline link =>
                $"[blue underline]{Markup.Escape(GetInlineText(link))}[/]" +
                (link.Url is not null ? $" [dim]({Markup.Escape(link.Url)})[/]" : ""),

            LineBreakInline lb when lb.IsHard => "\n",
            LineBreakInline => " ",

            HtmlInline => string.Empty,

            ContainerInline ci =>
                RenderInlines(ci, highlightSlashCommands),

            _ => string.Empty,
        };
    }

    /// <summary>
    /// Replace /command tokens that match known slash commands with colored markup.
    /// Only applied in prose (not in code blocks).
    /// </summary>
    private static string ColorizeSlashCommands(string escapedText)
    {
        // Walk word-by-word and replace /xxx tokens that match known commands
        var result = new StringBuilder(escapedText.Length);
        var i = 0;
        while (i < escapedText.Length)
        {
            if (escapedText[i] == '/' && (i == 0 || !char.IsLetterOrDigit(escapedText[i - 1])))
            {
                var end = i + 1;
                while (end < escapedText.Length && (char.IsLetterOrDigit(escapedText[end]) || escapedText[end] == '-'))
                    end++;
                var token = escapedText[i..end];
                if (KnownSlashCommands.Contains(token))
                {
                    result.Append($"[bold cyan]{token}[/]");
                    i = end;
                    continue;
                }
            }
            result.Append(escapedText[i++]);
        }
        return result.ToString();
    }

    // ── Utility ─────────────────────────────────────────────

    /// <summary>Extract plain text from an inline tree (no markup).</summary>
    private static string GetInlineText(ContainerInline? container)
    {
        if (container is null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit: sb.Append(lit.Content); break;
                case ContainerInline ci: sb.Append(GetInlineText(ci)); break;
                case CodeInline code: sb.Append(code.Content); break;
                case LineBreakInline lb: sb.Append(lb.IsHard ? '\n' : ' '); break;
            }
        }
        return sb.ToString();
    }

    // ── Syntax highlighting ─────────────────────────────────

    // Per-language keyword sets for lightweight highlighting
    private static readonly Dictionary<string, HashSet<string>> LangKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = ["abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "record", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
            "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "var", "virtual", "void", "volatile", "while"],
        ["cs"] = [],  // will alias below
        ["python"] = ["and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
            "elif", "else", "except", "False", "finally", "for", "from", "global", "if", "import", "in",
            "is", "lambda", "None", "nonlocal", "not", "or", "pass", "raise", "return", "True", "try",
            "while", "with", "yield"],
        ["javascript"] = ["async", "await", "break", "case", "catch", "class", "const", "continue", "debugger",
            "default", "delete", "do", "else", "export", "extends", "false", "finally", "for", "from",
            "function", "if", "import", "in", "instanceof", "let", "new", "null", "of", "return", "static",
            "super", "switch", "this", "throw", "true", "try", "typeof", "undefined", "var", "void",
            "while", "with", "yield"],
        ["typescript"] = [],  // alias below
        ["bash"] = ["if", "then", "else", "elif", "fi", "for", "while", "do", "done", "case", "esac",
            "function", "in", "return", "exit", "export", "readonly", "local", "source", "echo", "cd",
            "mkdir", "rm", "cp", "mv", "ls", "cat", "grep", "sed", "awk"],
        ["sh"] = [],
        ["sql"] = ["SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "ALTER",
            "TABLE", "INDEX", "VIEW", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON", "AS", "AND",
            "OR", "NOT", "IN", "IS", "NULL", "ORDER", "BY", "GROUP", "HAVING", "LIMIT", "OFFSET",
            "DISTINCT", "TOP", "COUNT", "SUM", "AVG", "MAX", "MIN"],
        ["go"] = ["break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough",
            "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range",
            "return", "select", "struct", "switch", "type", "var"],
        ["rust"] = ["as", "async", "await", "break", "const", "continue", "crate", "dyn", "else", "enum",
            "extern", "false", "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod", "move",
            "mut", "pub", "ref", "return", "self", "Self", "static", "struct", "super", "trait",
            "true", "type", "union", "unsafe", "use", "where", "while"],
        ["json"] = ["true", "false", "null"],
        ["yaml"] = ["true", "false", "null"],
    };

    static MarkdownRenderer()
    {
        // Aliases
        if (LangKeywords.TryGetValue("csharp", out var cs)) LangKeywords["cs"] = cs;
        if (LangKeywords.TryGetValue("javascript", out var js)) LangKeywords["typescript"] = js;
        if (LangKeywords.TryGetValue("bash", out var sh)) LangKeywords["sh"] = sh;
    }

    /// <summary>
    /// Apply simple token-level syntax highlighting to one code line.
    /// Returns a Spectre markup string.
    /// </summary>
    private static string HighlightCodeLine(string line, string lang)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        // Comments
        if (IsLineComment(line, lang))
            return $"[dim]{Markup.Escape(line)}[/]";

        if (!LangKeywords.TryGetValue(lang, out var keywords) || keywords.Count == 0)
            return Markup.Escape(line);

        // Token-by-token pass
        var result = new StringBuilder(line.Length + 32);
        var i = 0;
        while (i < line.Length)
        {
            // String literals — handle escape sequences so \"  \' \` don't end the literal early
            if (line[i] is '"' or '\'' or '`')
            {
                var quote = line[i];
                var end = i + 1;
                while (end < line.Length && line[end] != quote)
                {
                    if (line[end] == '\\' && end + 1 < line.Length)
                        end++; // skip the escaped character
                    end++;
                }
                if (end < line.Length) end++; // consume closing quote
                result.Append($"[yellow]{Markup.Escape(line[i..end])}[/]");
                i = end;
                continue;
            }

            // Identifiers/keywords
            if (char.IsLetter(line[i]) || line[i] == '_')
            {
                var end = i + 1;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_')) end++;
                var word = line[i..end];
                result.Append(keywords.Contains(word)
                    ? $"[blue]{Markup.Escape(word)}[/]"
                    : Markup.Escape(word));
                i = end;
                continue;
            }

            // Numbers
            if (char.IsDigit(line[i]))
            {
                var end = i + 1;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.')) end++;
                result.Append($"[green]{Markup.Escape(line[i..end])}[/]");
                i = end;
                continue;
            }

            result.Append(Markup.Escape(line[i++].ToString()));
        }

        return result.ToString();
    }

    private static bool IsLineComment(string line, string lang)
    {
        var trimmed = line.TrimStart();
        return lang switch
        {
            "bash" or "sh" or "python" or "yaml" => trimmed.StartsWith('#'),
            "sql" => trimmed.StartsWith("--", StringComparison.Ordinal),
            _ => trimmed.StartsWith("//", StringComparison.Ordinal) ||
                 trimmed.StartsWith("--", StringComparison.Ordinal),
        };
    }
}
