using System.Text.Json.Serialization;

namespace JD.AI.Channels.OpenClaw;

internal sealed class OpenClawOutboundMessage
{
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = "";

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = [];
}

internal sealed class OpenClawInboundMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("sender")]
    public string Sender { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";
}
