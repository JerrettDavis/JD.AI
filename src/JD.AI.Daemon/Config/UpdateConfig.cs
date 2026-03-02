#pragma warning disable CA2227 // Collection properties should be read only — needed for IOptions binding

namespace JD.AI.Daemon.Config;

/// <summary>
/// Configuration for the automatic update system.
/// Mapped from the "Updates" section in appsettings.json.
/// </summary>
public sealed class UpdateConfig
{
    /// <summary>Interval between automatic update checks. Default: 24 hours.</summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>When true, updates are applied automatically after detection. Default: false.</summary>
    public bool AutoApply { get; set; }

    /// <summary>When true, connected channels are notified about available updates.</summary>
    public bool NotifyChannels { get; set; } = true;

    /// <summary>When true, pre-release NuGet versions are considered.</summary>
    public bool PreRelease { get; set; }

    /// <summary>Maximum time to wait for agents to drain before forcing restart. Default: 30 seconds.</summary>
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>NuGet package ID to check for updates. Default: "JD.AI.Daemon".</summary>
    public string PackageId { get; set; } = "JD.AI.Daemon";

    /// <summary>NuGet feed URL. Default: official NuGet v3 API.</summary>
    public string NuGetFeedUrl { get; set; } = "https://api.nuget.org/v3-flatcontainer/";
}
