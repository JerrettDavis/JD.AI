namespace JD.AI.Core.Config;

/// <summary>
/// Controls which details are shown in the interactive welcome panel.
/// </summary>
public sealed record WelcomePanelSettings
{
    /// <summary>Show provider/model summary line.</summary>
    public bool ShowModelSummary { get; init; } = true;

    /// <summary>Show daemon and gateway indicators.</summary>
    public bool ShowServices { get; init; } = true;

    /// <summary>Show current working directory.</summary>
    public bool ShowWorkingDirectory { get; init; } = true;

    /// <summary>Show current JD.AI TUI version.</summary>
    public bool ShowVersion { get; init; } = true;

    /// <summary>Show message-of-the-day (MoTD) if available.</summary>
    public bool ShowMotd { get; init; }

    /// <summary>
    /// Optional MoTD source URL (for example a raw GitHub file).
    /// If empty, MoTD is skipped.
    /// </summary>
    public string? MotdUrl { get; init; }

    /// <summary>MoTD fetch timeout in milliseconds.</summary>
    public int MotdTimeoutMs { get; init; } = 700;

    /// <summary>Maximum rendered MoTD length.</summary>
    public int MotdMaxLength { get; init; } = 160;

    public static WelcomePanelSettings Normalize(WelcomePanelSettings? settings)
    {
        var value = settings ?? new WelcomePanelSettings();
        return value with
        {
            MotdTimeoutMs = Math.Clamp(value.MotdTimeoutMs, 100, 5_000),
            MotdMaxLength = Math.Clamp(value.MotdMaxLength, 40, 1_000),
            MotdUrl = string.IsNullOrWhiteSpace(value.MotdUrl) ? null : value.MotdUrl.Trim(),
        };
    }
}
