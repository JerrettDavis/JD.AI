using System.Text.Json.Serialization;

namespace JD.AI.Gateway.Client.Models;

public sealed class ChannelInfo
{
    [JsonPropertyName("channelType")]
    public string ChannelType { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }
}
