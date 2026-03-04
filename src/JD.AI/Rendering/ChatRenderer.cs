using JD.AI.Core.Config;
using Spectre.Console;

namespace JD.AI.Rendering;

/// <summary>
/// Renders chat messages, tool outputs, and status to the terminal
/// using Spectre.Console markup.
/// </summary>
public static class ChatRenderer
{
    /// <summary>Render the startup banner with detected providers.</summary>
    public static void RenderBanner(
        string modelName, string providerName, int totalModels)
    {
        var panel = new Panel(
            new Markup(
                $"[bold]jdai[/] — Semantic Kernel TUI Agent\n" +
                $"Provider: [cyan]{Markup.Escape(providerName)}[/] | " +
                $"Model: [green]{Markup.Escape(modelName)}[/] | " +
                $"Total models: {totalModels}\n" +
                $"Type [dim]/help[/] for commands, [dim]/quit[/] to exit."))
            .Border(BoxBorder.Rounded)
            .Header("[bold blue]Welcome[/]")
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>Render a user message.</summary>
    public static void RenderUserMessage(string text)
    {
        AnsiConsole.Markup("[bold cyan]❯[/] ");
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(text)}[/]");
    }

    /// <summary>
    /// Dim the already-visible user input line in-place.
    /// Moves the cursor up to the prompt line, rewrites it in dim,
    /// then returns to the next line for output.
    /// </summary>
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
        // Simple markdown-like rendering
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("```"))
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
        var header = string.IsNullOrWhiteSpace(args)
            ? $"» {toolName}"
            : $"» {toolName}({args})";

        // Truncate long results
        var displayResult = result.Length > 2000
            ? string.Concat(result.AsSpan(0, 2000), "\n... [truncated]")
            : result;

        var panel = new Panel(Markup.Escape(displayResult))
            .Header($"[bold]{Markup.Escape(header)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>Render a system/info message.</summary>
    public static void RenderInfo(string text)
    {
        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(text)}[/]");
    }

    /// <summary>Render a warning message.</summary>
    public static void RenderWarning(string text)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠ {Markup.Escape(text)}[/]");
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
        AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(text)}[/]");
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
    public static string? ReadInput(InteractiveInput? input = null)
    {
        AnsiConsole.Markup("[bold green]>[/] ");

        if (input != null)
            return input.ReadLine();

        return Console.ReadLine();
    }

    /// <summary>Prompt for user input, returning structured result with attachments.</summary>
    public static InputResult? ReadInputStructured(InteractiveInput input)
    {
        AnsiConsole.Markup("[bold green]>[/] ");
        return input.ReadLineWithAttachments();
    }

    /// <summary>Ask the user for confirmation.</summary>
    public static bool Confirm(string message)
    {
        AnsiConsole.Markup($"[yellow]{Markup.Escape(message)}[/] [dim]([green]y[/]/[red]n[/])[/] [dim](y)[/]: ");

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
        AnsiConsole.Markup("[bold magenta]◆[/] ");
    }

    /// <summary>Write a streaming text chunk (raw, inline).</summary>
    public static void WriteStreamingChunk(string text)
    {
        Console.Write(text);
    }

    /// <summary>End the streaming block.</summary>
    public static void EndStreaming()
    {
        Console.WriteLine();
        AnsiConsole.WriteLine();
    }

    // ── Thinking/reasoning rendering ─────────────────────

    /// <summary>Begin a thinking/reasoning block (dim gray output).</summary>
    public static void BeginThinking()
    {
        AnsiConsole.Markup("[dim]💭 [/]");
    }

    /// <summary>Write a thinking/reasoning chunk as dim gray text.</summary>
    public static void WriteThinkingChunk(string text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(text);
        Console.ResetColor();
    }

    /// <summary>End a thinking/reasoning block.</summary>
    public static void EndThinking()
    {
        Console.WriteLine();
    }

    // ── Turn metrics rendering ──────────────────────────────

    /// <summary>Render a turn completion metrics line, styled per <paramref name="style"/>.</summary>
    public static void RenderTurnMetrics(
        long elapsedMs, int tokens, long bytes,
        SpinnerStyle style = SpinnerStyle.Normal,
        long? ttftMs = null, string? model = null)
    {
        if (style == SpinnerStyle.None)
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

    private static string FormatElapsedMetric(long ms) =>
        ms >= 60_000
            ? $"{ms / 60_000}m {ms % 60_000 / 1000}s"
            : $"{ms / 1000.0:F1}s";

    private static string FormatBytes(long bytes) =>
        bytes switch
        {
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B",
        };
}
