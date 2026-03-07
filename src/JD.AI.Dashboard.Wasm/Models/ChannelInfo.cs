using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

public record ChannelInfo
{
    [JsonPropertyName("channelType")]
    public string ChannelType { get; init; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; init; }

    // Convenience aliases for UI code
    [JsonIgnore]
    public string Type => ChannelType;
    [JsonIgnore]
    public string Name => DisplayName;
    [JsonIgnore]
    public bool Connected => IsConnected;
}
