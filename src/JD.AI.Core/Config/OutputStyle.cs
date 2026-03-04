namespace JD.AI.Core.Config;

/// <summary>
/// Controls how assistant output is rendered in the terminal.
/// </summary>
public enum OutputStyle
{
    /// <summary>Rich markdown-like rendering with styling.</summary>
    Rich,

    /// <summary>Plain text rendering without formatting.</summary>
    Plain,

    /// <summary>Reduced whitespace and compact formatting.</summary>
    Compact,

    /// <summary>Structured JSON output.</summary>
    Json,
}
