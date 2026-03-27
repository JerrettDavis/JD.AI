using System.Text.Json.Serialization;

namespace JD.AI.Gateway.Client.Models;

public sealed class SessionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }

    [JsonPropertyName("providerName")]
    public string? ProviderName { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("messageCount")]
    public int MessageCount { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("turns")]
    public IList<TurnRecord> Turns { get; init; } = [];
}

public sealed class TurnRecord
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("turnIndex")]
    public int TurnIndex { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }

    [JsonPropertyName("tokensIn")]
    public int TokensIn { get; init; }

    [JsonPropertyName("tokensOut")]
    public int TokensOut { get; init; }

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }
}
