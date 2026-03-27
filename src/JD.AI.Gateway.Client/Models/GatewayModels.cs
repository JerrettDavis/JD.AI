using System.Text.Json.Serialization;

namespace JD.AI.Gateway.Client.Models;

public sealed class GatewayStatus
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("uptime")]
    public DateTimeOffset Uptime { get; init; }

    [JsonPropertyName("channels")]
    public GatewayChannelStatus[] Channels { get; init; } = [];

    [JsonPropertyName("agents")]
    public GatewayAgentStatus[] Agents { get; init; } = [];

    [JsonPropertyName("routes")]
    public IDictionary<string, string> Routes { get; init; } = new Dictionary<string, string>();

    [JsonIgnore]
    public bool IsRunning => string.Equals(Status, "running", StringComparison.Ordinal);

    [JsonIgnore]
    public int ActiveAgents => Agents.Length;

    [JsonIgnore]
    public int ActiveChannels => Channels.Count(c => c.IsConnected);
}

public sealed record GatewayChannelStatus
{
    [JsonPropertyName("channelType")]
    public string ChannelType { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }
}

public sealed record GatewayAgentStatus
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;
}

public sealed record RoutingMapping(string ChannelId, string AgentId);

public sealed record ActivityEvent
{
    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
