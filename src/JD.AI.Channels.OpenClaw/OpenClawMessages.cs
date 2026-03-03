using System.Text.Json.Serialization;

namespace JD.AI.Channels.OpenClaw;

/// <summary>OpenClaw session summary from sessions.list.</summary>
public sealed class OpenClawSession
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("modelProvider")]
    public string? ModelProvider { get; set; }

    [JsonPropertyName("totalTokens")]
    public long TotalTokens { get; set; }

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }
}

/// <summary>OpenClaw channel status from channels.status.</summary>
public sealed class OpenClawChannelStatus
{
    [JsonPropertyName("configured")]
    public bool Configured { get; set; }

    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("lastError")]
    public string? LastError { get; set; }
}
