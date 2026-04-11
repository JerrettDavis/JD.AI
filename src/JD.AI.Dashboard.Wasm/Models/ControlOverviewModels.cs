namespace JD.AI.Dashboard.Wasm.Models;

/// <summary>Derived display model for the Control > Overview page.</summary>
public sealed record ControlOverviewSnapshotModel
{
    public string StatusText { get; init; } = "Disconnected";
    public string StatusColor { get; init; } = "error";   // MudBlazor color name
    public string UptimeDisplay { get; init; } = "—";
    public string TickInterval { get; init; } = "—";
    public string LastChannelsRefresh { get; init; } = "—";
    public string CostDisplay { get; init; } = "$0.00";
    public int TokenCount { get; init; }
    public int MessageCount { get; init; }
    public int SessionCount { get; init; }
    public int ActiveAgents { get; init; }
    public int ActiveChannels { get; init; }

    public static ControlOverviewSnapshotModel From(GatewayStatus? status, SessionInfo[] sessions)
    {
        if (status is null)
            return new ControlOverviewSnapshotModel();

        var uptimeSpan = DateTimeOffset.UtcNow - status.Uptime;
        var uptimeDisplay = uptimeSpan.TotalDays >= 1
            ? $"{(int)uptimeSpan.TotalDays}d {uptimeSpan.Hours}h"
            : uptimeSpan.TotalHours >= 1
                ? $"{(int)uptimeSpan.TotalHours}h"
                : $"{(int)uptimeSpan.TotalMinutes}m";

        return new ControlOverviewSnapshotModel
        {
            StatusText = string.IsNullOrWhiteSpace(status.Status) ? "Unknown"
                : status.Status.Equals("running", StringComparison.OrdinalIgnoreCase) ? "OK"
                : status.Status,
            StatusColor = status.IsRunning ? "success" : "warning",
            UptimeDisplay = uptimeDisplay,
            TickInterval = "30s",       // gateway default; extend if API exposes it
            LastChannelsRefresh = "just now",
            CostDisplay = "$0.00",
            SessionCount = sessions.Length,
            ActiveAgents = status.ActiveAgents,
            ActiveChannels = status.ActiveChannels,
        };
    }
}

/// <summary>Recent session row for the overview table.</summary>
public sealed record RecentSessionRow
{
    public string SessionKey { get; init; } = "";
    public string Model { get; init; } = "";
    public string TimeAgo { get; init; } = "";

    public static RecentSessionRow From(SessionInfo s)
    {
        var elapsed = DateTimeOffset.UtcNow - s.UpdatedAt;
        var timeAgo = elapsed.TotalDays >= 1 ? $"{(int)elapsed.TotalDays}d ago"
            : elapsed.TotalHours >= 1 ? $"{(int)elapsed.TotalHours}h ago"
            : elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m ago"
            : "just now";

        return new RecentSessionRow
        {
            SessionKey = s.Id,
            Model = s.ModelId ?? "—",
            TimeAgo = timeAgo,
        };
    }
}

public sealed class GatewayAccessSettings
{
    public string WebSocketUrl { get; set; } = "";
    public string GatewayToken { get; set; } = "";
    public string DefaultSessionKey { get; set; } = "";
    public string Language { get; set; } = "en";

    public static readonly string[] SupportedLanguages =
    [
        "en", "zh-Hans", "zh-Hant", "pt", "de", "es", "ja", "ko", "fr", "tr", "uk", "id", "pl"
    ];

    public static readonly string[] LanguageDisplayNames =
    [
        "English", "简体中文", "繁體中文", "Português", "Deutsch",
        "Español", "日本語", "한국어", "Français", "Türkçe",
        "Українська", "Bahasa Indonesia", "Polski"
    ];
}
