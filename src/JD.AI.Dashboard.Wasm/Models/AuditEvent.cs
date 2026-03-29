using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

public record AuditEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("level")]
    public string Level { get; init; } = "info";

    [JsonPropertyName("agentId")]
    public string? AgentId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("payload")]
    public string? Payload { get; init; }
}
