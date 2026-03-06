namespace JD.AI.Rendering;

/// <summary>
/// Visual state level for a welcome-banner service indicator.
/// </summary>
public enum IndicatorState
{
    Healthy,
    Warning,
    Error,
    Neutral,
}

/// <summary>
/// A concise, color-coded status indicator shown in the welcome banner.
/// </summary>
public sealed record WelcomeIndicator(string Name, string Value, IndicatorState State);
