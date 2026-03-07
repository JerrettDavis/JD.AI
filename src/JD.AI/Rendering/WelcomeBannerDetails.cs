namespace JD.AI.Rendering;

/// <summary>
/// Optional contextual details rendered in the startup welcome panel.
/// </summary>
public sealed record WelcomeBannerDetails(
    string? WorkingDirectory = null,
    string? Version = null,
    string? Motd = null);
