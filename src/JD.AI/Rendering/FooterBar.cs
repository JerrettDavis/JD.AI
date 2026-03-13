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

        var segments = state.ToSegments();
        var rendered = _template.Render(
            new Dictionary<string, string?>(
                (IDictionary<string, string?>)segments));

        // Escape any Spectre markup characters in the rendered text
        var escaped = Markup.Escape(rendered);

        return new Markup($"[on grey]{escaped}[/]");
    }
}
