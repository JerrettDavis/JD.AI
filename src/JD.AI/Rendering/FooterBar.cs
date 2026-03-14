using Spectre.Console;
using Spectre.Console.Rendering;

namespace JD.AI.Rendering;

/// <summary>
/// Renders a <see cref="FooterState"/> snapshot into a Spectre.Console <see cref="IRenderable"/>
/// using a parsed <see cref="FooterTemplate"/>.
/// </summary>
public sealed class FooterBar
{
    private static readonly Lock RenderLock = new();
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

        lock (RenderLock)
        {
            int width;
            int windowHeight;
            int windowTop;
            int savedLeft;
            int savedTop;
            try
            {
                width = Console.WindowWidth;
                windowHeight = Console.WindowHeight;
                windowTop = Console.WindowTop;
                (savedLeft, savedTop) = Console.GetCursorPosition();
            }
            catch (IOException)
            {
                return;
            }

            if (width <= 1 || windowHeight <= 0)
                return;

            var footerTop = windowTop + windowHeight - 1;
            var maxChars = Math.Max(1, width - 1); // avoid autowrap-induced scroll
            var line = BuildPaddedLine(state);
            if (line.Length > maxChars)
                line = line[..maxChars];
            else if (line.Length < maxChars)
                line = line.PadRight(maxChars);

            try
            {
                Console.SetCursorPosition(0, footerTop);
                Console.Write(line);

                // Restore original cursor, clamped to visible content area above footer.
                var restoreTop = Math.Min(savedTop, Math.Max(windowTop, footerTop - 1));
                var restoreLeft = Math.Max(0, Math.Min(savedLeft, width - 1));
                Console.SetCursorPosition(restoreLeft, restoreTop);
            }
            catch (IOException)
            {
                // Ignore footer failures in non-interactive / constrained terminals.
            }
        }
    }

    private string BuildPaddedLine(FooterState state)
    {
        var segments = state.ToSegments();
        var rendered = _template.Render(
            new Dictionary<string, string?>(
                (IDictionary<string, string?>)segments));

        int width;
        try
        {
            width = Console.WindowWidth;
        }
        catch (IOException)
        {
            width = 0;
        }

        return width > 0 ? rendered.PadRight(width) : rendered;
    }
}
