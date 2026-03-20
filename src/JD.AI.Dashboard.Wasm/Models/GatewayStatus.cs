using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

public record GatewayStatus
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("uptime")]
    public DateTimeOffset Uptime { get; init; }

    [JsonPropertyName("channels")]
    public GatewayChannelStatus[] Channels { get; init; } = [];

    [JsonPropertyName("agents")]
    public GatewayAgentStatus[] Agents { get; init; } = [];

    [JsonPropertyName("routes")]
    public IDictionary<string, string> Routes { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("openClaw")]
    public OpenClawStatus? OpenClaw { get; init; }

    // Computed convenience properties for the UI
    [JsonIgnore]
    public bool IsRunning => string.Equals(Status, "running", StringComparison.Ordinal);
    [JsonIgnore]
    public int ActiveAgents => Agents.Length;
    [JsonIgnore]
    public int ActiveChannels => Channels.Count(c => c.IsConnected);
    [JsonIgnore]
    public int ActiveSessions => 0; // Not yet exposed by the API
}

public record GatewayChannelStatus
{
    [JsonPropertyName("channelType")]
    public string ChannelType { get; init; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }
}

public record GatewayAgentStatus
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "";

    [JsonPropertyName("model")]
    public string Model { get; init; } = "";
}

public record OpenClawStatus
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("connected")]
    public bool ConnectedRaw { get; init; }

    [JsonPropertyName("defaultMode")]
    public string? DefaultMode { get; init; }

    [JsonPropertyName("overrideActive")]
    public bool OverrideActive { get; init; }

    [JsonPropertyName("overrideChannels")]
    public string[] OverrideChannels { get; init; } = [];

    [JsonPropertyName("registeredAgents")]
    public string[] RegisteredAgents { get; init; } = [];

    // Computed
    [JsonIgnore]
    public bool Connected => ConnectedRaw || Enabled;
    [JsonIgnore]
    public int RegisteredAgentCount => RegisteredAgents.Length;
}

public record ActivityEvent
{
    public string EventType { get; init; } = "";
    public string SourceId { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public string? Message { get; init; }
}
