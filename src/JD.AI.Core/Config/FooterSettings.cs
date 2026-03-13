namespace JD.AI.Core.Config;

/// <summary>
/// Controls the visibility and layout of a segment in the TUI footer.
/// </summary>
public sealed record SegmentVisibilityOverride
{
    /// <summary>
    /// Visibility mode for the segment. Valid values: "always", "auto", "never".
    /// </summary>
    public string Visible { get; init; } = "auto";

    /// <summary>
    /// Optional context-window percentage threshold at which this segment switches to warn styling.
    /// When null, the footer-level <see cref="FooterSettings.WarnThresholdPercent"/> is used.
    /// </summary>
    public int? WarnPercent { get; init; }
}

/// <summary>
/// Controls the TUI status footer rendered at the bottom of the interactive session.
/// </summary>
public sealed record FooterSettings
{
    private const string DefaultTemplate =
        "{folder} │ {branch?} │ {pr?} │ {context} │ {provider} │ {model} │ {turns}";

    /// <summary>When false, the footer bar is hidden entirely.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Number of rows the footer occupies (1–3).</summary>
    public int Lines { get; init; } = 1;

    /// <summary>
    /// Mustache-style template that controls which segments appear and in what order.
    /// Segment tokens are wrapped in braces; append <c>?</c> to make a segment optional.
    /// </summary>
    public string Template { get; init; } = DefaultTemplate;

    /// <summary>
    /// Percentage of the context window remaining at which the context segment switches to warn styling (1–50).
    /// Default: 15.
    /// </summary>
    public int WarnThresholdPercent { get; init; } = 15;

    /// <summary>Per-segment visibility overrides, keyed by segment name.</summary>
    public Dictionary<string, SegmentVisibilityOverride> Segments { get; init; } = [];

    /// <summary>
    /// Returns a normalised copy of <paramref name="settings"/>, applying defaults and clamping
    /// out-of-range values. Passing <see langword="null"/> returns a fully-defaulted instance.
    /// </summary>
    public static FooterSettings Normalize(FooterSettings? settings)
    {
        var value = settings ?? new FooterSettings();
        return value with
        {
            Lines = Math.Clamp(value.Lines, 1, 3),
            WarnThresholdPercent = Math.Clamp(value.WarnThresholdPercent, 1, 50),
            Template = string.IsNullOrWhiteSpace(value.Template) ? DefaultTemplate : value.Template,
            Segments = value.Segments ?? [],
        };
    }
}
