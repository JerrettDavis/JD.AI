using System.Text.Json.Serialization;

namespace JD.AI.Gateway.Client.Models;

public sealed record AgentInfo(
    string Id,
    string Provider,
    string Model,
    int TurnCount,
    DateTimeOffset CreatedAt
);

public sealed class AgentDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("systemPrompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [JsonPropertyName("autoSpawn")]
    public bool AutoSpawn { get; set; }

    [JsonPropertyName("maxTurns")]
    public int MaxTurns { get; set; }

    [JsonPropertyName("tools")]
    public string[] Tools { get; set; } = [];

    [JsonPropertyName("parameters")]
    public ModelParameters? Parameters { get; set; }
}

public sealed class ModelParameters
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("topP")]
    public double? TopP { get; set; }

    [JsonPropertyName("topK")]
    public int? TopK { get; set; }

    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("contextWindowSize")]
    public int? ContextWindowSize { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("stopSequences")]
    public string[] StopSequences { get; set; } = [];
}

public sealed record AgentStreamChunk(
    string Type,
    string AgentId,
    string? Content
);
