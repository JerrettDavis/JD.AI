using Spectre.Console;
using Spectre.Console.Rendering;

namespace JD.AI.Rendering;

/// <summary>
/// Renders a <see cref="FooterState"/> snapshot into a Spectre.Console <see cref="IRenderable"/>
/// using a parsed <see cref="FooterTemplate"/>.
/// </summary>
public sealed class FooterBar
{
    private readonly FooterTemplate _template;
    private readonly bool _enabled;

    /// <summary>
    /// Creates a new <see cref="FooterBar"/> with the given template string.
    /// </summary>
    /// <param name="template">Template string (e.g., <c>{folder} │ {branch?} │ {context}</c>).</param>
    /// <param name="enabled">Whether the footer is enabled. When <see langword="false"/>,
    /// <see cref="ToRenderable"/> returns an empty <see cref="Text"/>.</param>
    public FooterBar(string template, bool enabled = true)
    {
        _template = FooterTemplate.Parse(template);
        _enabled = enabled;
    }

    /// <summary>
    /// Renders the current <paramref name="state"/> into an <see cref="IRenderable"/>.
    /// Returns an empty <see cref="Text"/> when the footer is disabled.
    /// </summary>
    public IRenderable ToRenderable(FooterState state)
    {
        if (!_enabled)
            return new Text(string.Empty);

        var padded = BuildPaddedLine(state);

        // Escape any Spectre markup characters in the rendered text
        var escaped = Markup.Escape(padded);

        return new Markup($"[on grey]{escaped}[/]");
    }

    /// <summary>
    /// Renders the footer as a persistent status line pinned to the bottom terminal row.
    /// </summary>
    public void RenderPersistent(FooterState state)
    {
        if (!_enabled)
            return;

        int width;
        int height;
        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
        }
        catch (IOException)
        {
            return;
        }

        if (width <= 0 || height <= 0)
            return;

        var line = BuildPaddedLine(state);
        if (line.Length > width)
            line = line[..width];

        // Save cursor, draw footer on the last row, then restore cursor.
        Console.Write("\x1b7");
        Console.Write($"\x1b[{height};1H");
        Console.Write("\x1b[2K");
        Console.Write(line);
        Console.Write("\x1b8");
    }

    private string BuildPaddedLine(FooterState state)
    {
        var segments = state.ToSegments();
        var rendered = _template.Render(
            new Dictionary<string, string?>(
                (IDictionary<string, string?>)segments));

        int width;
        try { width = Console.WindowWidth; }
        catch (IOException) { width = 0; }

        return width > 0 ? rendered.PadRight(width) : rendered;
    }
}
