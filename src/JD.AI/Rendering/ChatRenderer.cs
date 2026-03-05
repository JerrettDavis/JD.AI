using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JD.AI.Core.Config;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Renders chat messages, tool outputs, and status to the terminal
/// using Spectre.Console markup.
/// </summary>
public static class ChatRenderer
{
    private sealed record ThemePalette(
        string HeaderColor,
        string PromptColor,
        string InfoColor,
        string WarningColor,
        string ErrorColor,
        string UserTextColor,
        string ThinkingColor);

    private static readonly Dictionary<TuiTheme, ThemePalette> ThemePalettes =
        new()
        {
            [TuiTheme.DefaultDark] = new("#4ea1ff", "#67d8ef", "#9aa0a6", "#f2c14e", "#ff6b6b", "#9aa0a6", "#7f8c8d"),
            [TuiTheme.Monokai] = new("#a6e22e", "#66d9ef", "#a1a1a1", "#e6db74", "#f92672", "#bdbdbd", "#75715e"),
            [TuiTheme.SolarizedDark] = new("#268bd2", "#2aa198", "#93a1a1", "#b58900", "#dc322f", "#93a1a1", "#586e75"),
            [TuiTheme.SolarizedLight] = new("#268bd2", "#2aa198", "#657b83", "#b58900", "#dc322f", "#657b83", "#839496"),
            [TuiTheme.Nord] = new("#81a1c1", "#88c0d0", "#d8dee9", "#ebcb8b", "#bf616a", "#d8dee9", "#4c566a"),
            [TuiTheme.Dracula] = new("#bd93f9", "#8be9fd", "#f8f8f2", "#f1fa8c", "#ff5555", "#f8f8f2", "#6272a4"),
            [TuiTheme.OneDark] = new("#61afef", "#56b6c2", "#abb2bf", "#e5c07b", "#e06c75", "#abb2bf", "#5c6370"),
            [TuiTheme.CatppuccinMocha] = new("#89b4fa", "#94e2d5", "#cdd6f4", "#f9e2af", "#f38ba8", "#cdd6f4", "#6c7086"),
            [TuiTheme.Gruvbox] = new("#83a598", "#8ec07c", "#d5c4a1", "#fabd2f", "#fb4934", "#d5c4a1", "#928374"),
            [TuiTheme.HighContrast] = new("#ffffff", "#00ffff", "#ffffff", "#ffff00", "#ff0000", "#ffffff", "#aaaaaa"),
        };

    private static ThemePalette _palette = ThemePalettes[TuiTheme.DefaultDark];
    private static bool _jsonStreamingActive;

    public static TuiTheme CurrentTheme { get; private set; } = TuiTheme.DefaultDark;
    public static OutputStyle CurrentOutputStyle { get; private set; } = OutputStyle.Rich;

    /// <summary>Apply a named terminal theme for future output.</summary>
    public static void ApplyTheme(TuiTheme theme)
    {
        CurrentTheme = theme;
        _palette = ThemePalettes.TryGetValue(theme, out var selected)
            ? selected
            : ThemePalettes[TuiTheme.DefaultDark];
    }

    /// <summary>Set assistant output rendering style.</summary>
    public static void SetOutputStyle(OutputStyle style)
    {
        CurrentOutputStyle = style;
    }

    /// <summary>Render the startup banner with detected providers.</summary>
    public static void RenderBanner(
        string modelName, string providerName, int totalModels)
    {
        var panel = new Panel(
            new Markup(
                $"[bold]jdai[/] — Semantic Kernel TUI Agent\n" +
                $"Provider: [#{_palette.PromptColor.TrimStart('#')}]{Markup.Escape(providerName)}[/] | " +
                $"Model: [green]{Markup.Escape(modelName)}[/] | " +
                $"Total models: {totalModels}\n" +
                $"Type [dim]/help[/] for commands, [dim]/quit[/] to exit."))
            .Border(BoxBorder.Rounded)
            .Header($"[bold {_palette.HeaderColor}]Welcome[/]")
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>Render a user message.</summary>
    public static void RenderUserMessage(string text)
    {
        AnsiConsole.Markup($"[bold {_palette.PromptColor}]❯[/] ");
        AnsiConsole.MarkupLine($"[{_palette.UserTextColor}]{Markup.Escape(text)}[/]");
    }

    /// <summary>
    /// Dim the already-visible user input line in-place.
    /// Moves the cursor up to the prompt line, rewrites it in dim,
    /// then returns to the next line for output.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static void DimInputLine(string text)
    {
        try
        {
            // Move up one line (the cursor is at the start of the next line after Enter)
            // Erase the line, rewrite in dim with the same "> " prefix
            Console.Write("\x1b[1A\x1b[2K\r\x1b[2m> ");
            Console.Write(text);
            Console.Write("\x1b[0m\n");
        }
        catch (ObjectDisposedException)
        {
            // Console torn down during shutdown
        }
    }

    /// <summary>Render an assistant text response.</summary>
    public static void RenderAssistantMessage(string text)
    {
        switch (CurrentOutputStyle)
        {
            case OutputStyle.Plain:
                Console.WriteLine(text);
                Console.WriteLine();
                return;

            case OutputStyle.Compact:
                var compact = string.Join(
                    '\n',
                    text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
                Console.WriteLine(compact);
                Console.WriteLine();
                return;

            case OutputStyle.Json:
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    type = "assistant",
                    content = text,
                }));
                Console.WriteLine();
                return;

            default:
                break;
        }

        // Rich markdown-like rendering
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                AnsiConsole.MarkupLine("[dim]" + Markup.Escape(line) + "[/]");
            }
            else if (line.StartsWith('#'))
            {
                AnsiConsole.MarkupLine("[bold yellow]" + Markup.Escape(line) + "[/]");
            }
            else
            {
                AnsiConsole.WriteLine(line);
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>Render a tool invocation and its result.</summary>
    public static void RenderToolCall(string toolName, string? args, string result)
    {
        if (CurrentOutputStyle == OutputStyle.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                type = "tool_call",
                tool = toolName,
                arguments = args,
                result = result.Length > 2000
                    ? string.Concat(result.AsSpan(0, 2000), "\n... [truncated]")
                    : result,
            }));
            return;
        }

        // Compact tool-call display used by rich output mode
        var header = string.IsNullOrWhiteSpace(args)
            ? toolName
            : $"{toolName}({args})";

        // Truncate long results
        var displayResult = result.Length > 2000
            ? string.Concat(result.AsSpan(0, 2000), "\n... [truncated]")
            : result;

        var resultLines = displayResult.Split('\n');
        var lineCount = resultLines.Length;

        if (CurrentOutputStyle == OutputStyle.Rich)
        {
            AnsiConsole.MarkupLine($"  [dim]●[/] [bold]{Markup.Escape(header)}[/]");

            if (lineCount <= 3)
            {
                foreach (var line in resultLines)
                {
                    AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(line)}[/]");
                }
            }
            else
            {
                // Show first line + collapse indicator
                AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(resultLines[0])}[/]");
                AnsiConsole.MarkupLine($"    [dim]└ {lineCount} lines...[/]");
            }

            return;
        }

        // Plain/Compact: simple prefix
        Console.WriteLine($"  ● {header}");
        if (lineCount <= 5)
        {
            foreach (var line in resultLines)
                Console.WriteLine($"    {line}");
        }
        else
        {
            Console.WriteLine($"    {resultLines[0]}");
            Console.WriteLine($"    └ {lineCount} lines...");
        }
    }

    /// <summary>Render a system/info message.</summary>
    public static void RenderInfo(string text)
    {
        if (CurrentOutputStyle == OutputStyle.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { type = "info", content = text }));
            return;
        }

        AnsiConsole.MarkupLine($"[{_palette.InfoColor}]{Markup.Escape(text)}[/]");
    }

    /// <summary>Render a warning message.</summary>
    public static void RenderWarning(string text)
    {
        if (CurrentOutputStyle == OutputStyle.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { type = "warning", content = text }));
            return;
        }

        AnsiConsole.MarkupLine($"[{_palette.WarningColor}]⚠ {Markup.Escape(text)}[/]");
    }

    /// <summary>Render a system prompt size warning below the welcome banner.</summary>
    public static void RenderSystemPromptWarning(int actualTokens, int budgetTokens, int budgetPercent, int contextWindow)
    {
        var actualK = actualTokens / 1000.0;
        var budgetK = budgetTokens / 1000.0;
        AnsiConsole.MarkupLine(
            $"[yellow]⚠ Large system prompt may degrade performance " +
            $"({actualK:F1}k tokens exceeds {budgetPercent}%/{budgetK:F0}k budget of {contextWindow / 1000}k context window).[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Render an error message.</summary>
    public static void RenderError(string text)
    {
        if (CurrentOutputStyle == OutputStyle.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { type = "error", content = text }));
            return;
        }

        AnsiConsole.MarkupLine($"[{_palette.ErrorColor}]✗ {Markup.Escape(text)}[/]");
    }

    /// <summary>Render the status bar line.</summary>
    public static void RenderStatusBar(
        string provider, string model, long tokens)
    {
        var width = Console.WindowWidth;
        var status = $" {provider} │ {model} │ {tokens:N0} tokens │ /help ";

        if (status.Length < width)
        {
            status = status.PadRight(width);
        }

        AnsiConsole.Markup($"[on grey]{Markup.Escape(status)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Prompt for user input with interactive completions.</summary>
    [ExcludeFromCodeCoverage]
    public static string? ReadInput(InteractiveInput? input = null)
    {
        AnsiConsole.Markup($"[bold {_palette.PromptColor}]>[/] ");

        if (input != null)
            return input.ReadLine();

        return Console.ReadLine();
    }

    /// <summary>Prompt for user input, returning structured result with attachments.</summary>
    [ExcludeFromCodeCoverage]
    public static InputResult? ReadInputStructured(InteractiveInput input)
    {
        AnsiConsole.Markup($"[bold {_palette.PromptColor}]>[/] ");
        return input.ReadLineWithAttachments();
    }

    /// <summary>Ask the user for confirmation.</summary>
    [ExcludeFromCodeCoverage]
    public static bool Confirm(string message)
    {
        AnsiConsole.Markup($"[{_palette.WarningColor}]{Markup.Escape(message)}[/] [dim]([green]y[/]/[red]n[/])[/] [dim](y)[/]: ");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.Y:
                case ConsoleKey.Enter:
                    AnsiConsole.MarkupLine("[green]y[/]");
                    return true;

                case ConsoleKey.N:
                case ConsoleKey.Escape:
                    AnsiConsole.MarkupLine("[red]n[/]");
                    return false;

                default:
                    // ignore other keys
                    break;
            }
        }
    }

    // ── Streaming rendering ────────────────────────────────

    /// <summary>Begin an assistant streaming block (response content).</summary>
    public static void BeginStreaming()
    {
        _jsonStreamingActive = CurrentOutputStyle == OutputStyle.Json;
        if (_jsonStreamingActive)
        {
            Console.Write("{\"type\":\"assistant\",\"content\":\"");
            return;
        }

        if (CurrentOutputStyle == OutputStyle.Rich)
            AnsiConsole.Markup($"[bold {_palette.PromptColor}]◆[/] ");
    }

    /// <summary>Write a streaming text chunk (raw, inline).</summary>
    public static void WriteStreamingChunk(string text)
    {
        if (_jsonStreamingActive)
        {
            Console.Write(EscapeJsonString(text));
            return;
        }

        Console.Write(text);
    }

    /// <summary>End the streaming block.</summary>
    public static void EndStreaming()
    {
        if (_jsonStreamingActive)
        {
            Console.WriteLine("\"}");
            Console.WriteLine();
            _jsonStreamingActive = false;
            return;
        }

        Console.WriteLine();
        AnsiConsole.WriteLine();
    }

    // ── Thinking/reasoning rendering ─────────────────────

    /// <summary>Begin a thinking/reasoning block (dim gray output).</summary>
    public static void BeginThinking()
    {
        if (CurrentOutputStyle == OutputStyle.Json)
            return;

        if (CurrentOutputStyle == OutputStyle.Rich)
            AnsiConsole.Markup($"[{_palette.ThinkingColor}]💭 [/]");
    }

    /// <summary>Write a thinking/reasoning chunk as dim gray text.</summary>
    public static void WriteThinkingChunk(string text)
    {
        if (CurrentOutputStyle == OutputStyle.Json)
            return;

        if (CurrentOutputStyle == OutputStyle.Rich)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(text);
            Console.ResetColor();
            return;
        }

        Console.Write(text);
    }

    /// <summary>End a thinking/reasoning block.</summary>
    public static void EndThinking()
    {
        if (CurrentOutputStyle == OutputStyle.Json)
            return;

        Console.WriteLine();
    }

    // ── Turn metrics rendering ──────────────────────────────

    /// <summary>Render a turn completion metrics line, styled per <paramref name="style"/>.</summary>
    public static void RenderTurnMetrics(
        long elapsedMs, int tokens, long bytes,
        SpinnerStyle style = SpinnerStyle.Normal,
        long? ttftMs = null, string? model = null)
    {
        if (style == SpinnerStyle.None || CurrentOutputStyle == OutputStyle.Json)
            return;

        var elapsed = FormatElapsedMetric(elapsedMs);
        var size = FormatBytes(bytes);

        var line = style switch
        {
            SpinnerStyle.Minimal =>
                $"[dim]  ── {Markup.Escape(elapsed)} ──[/]",

            SpinnerStyle.Normal =>
                $"[dim]  ── {Markup.Escape(elapsed)} │ {tokens:N0} tokens │ {Markup.Escape(size)} ──[/]",

            SpinnerStyle.Rich =>
                FormatRichMetrics(elapsed, tokens, size, elapsedMs),

            SpinnerStyle.Nerdy =>
                FormatNerdyMetrics(elapsed, tokens, size, elapsedMs, ttftMs, model),

            _ =>
                $"[dim]  ── {Markup.Escape(elapsed)} │ {tokens:N0} tokens │ {Markup.Escape(size)} ──[/]",
        };

        AnsiConsole.MarkupLine(line);
    }

    private static string FormatRichMetrics(
        string elapsed, int tokens, string size, long elapsedMs)
    {
        var tokPerSec = elapsedMs > 0
            ? $"{tokens / (elapsedMs / 1000.0):F1} tok/s"
            : "—";

        return $"[dim]  ── {Markup.Escape(elapsed)} │ {tokens:N0} tokens │ " +
               $"{Markup.Escape(size)} │ {Markup.Escape(tokPerSec)} ──[/]";
    }

    private static string FormatNerdyMetrics(
        string elapsed, int tokens, string size, long elapsedMs,
        long? ttftMs, string? model)
    {
        var tokPerSec = elapsedMs > 0
            ? $"{tokens / (elapsedMs / 1000.0):F1} tok/s"
            : "—";
        var ttft = ttftMs.HasValue
            ? $"ttft: {ttftMs.Value / 1000.0:F2}s"
            : "ttft: —";
        var modelPart = !string.IsNullOrEmpty(model)
            ? $" │ [yellow]{Markup.Escape(model)}[/]"
            : "";

        return $"[dim]  ── {Markup.Escape(elapsed)} │ {tokens:N0} tok │ " +
               $"{Markup.Escape(size)} │ {Markup.Escape(tokPerSec)} │ " +
               $"{Markup.Escape(ttft)}{modelPart} ──[/]";
    }

    internal static string FormatElapsedMetric(long ms) =>
        ms >= 60_000
            ? $"{ms / 60_000}m {ms % 60_000 / 1000}s"
            : $"{ms / 1000.0:F1}s";

    internal static string FormatBytes(long bytes) =>
        bytes switch
        {
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B",
        };

    internal static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return JsonEncodedText.Encode(value).ToString();
    }
}
