namespace JD.AI.Core.Config;

/// <summary>
/// Controls the verbosity and visual style of the thinking/loading progress indicator.
/// </summary>
public enum SpinnerStyle
{
    /// <summary>No animation or progress display.</summary>
    None,

    /// <summary>Single dot with elapsed time only.</summary>
    Minimal,

    /// <summary>Braille spinner with elapsed time and live token count. Default.</summary>
    Normal,

    /// <summary>Spinner with progress bar, tokens, bytes, and throughput.</summary>
    Rich,

    /// <summary>All statistics including model name, time-to-first-token, and internals.</summary>
    Nerdy,
}
