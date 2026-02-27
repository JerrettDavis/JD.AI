using Spectre.Console;

namespace JD.AI.Tui.Rendering;

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
            ? $"🔧 {toolName}"
            : $"🔧 {toolName}({args})";

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
        return AnsiConsole.Confirm($"[yellow]{Markup.Escape(message)}[/]");
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
}
