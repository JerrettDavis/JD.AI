namespace JD.AI.Channels.OpenClaw;

/// <summary>
/// Configuration for connecting to an OpenClaw instance.
/// </summary>
public sealed class OpenClawConfig
{
    /// <summary>Base URL of the OpenClaw HTTP API.</summary>
    public string BaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Friendly name for this OpenClaw instance (used in <see cref="OpenClawBridgeChannel.DisplayName"/>).</summary>
    public string InstanceName { get; set; } = "local";

    /// <summary>Optional API key for authenticating with OpenClaw.</summary>
    public string? ApiKey { get; set; }

    /// <summary>OpenClaw channel to send outbound messages to.</summary>
    public string TargetChannel { get; set; } = "default";

    /// <summary>OpenClaw channel to poll for inbound messages.</summary>
    public string SourceChannel { get; set; } = "default";

    /// <summary>Interval in milliseconds between message polls.</summary>
    public int PollIntervalMs { get; set; } = 1000;
}
