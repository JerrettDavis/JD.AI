namespace JD.AI.Dashboard.Wasm.Models;

public record SessionInfo
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public string? ModelId { get; init; }
    public string? ProviderName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int TotalTokens { get; init; }
    public int MessageCount { get; init; }
    public bool IsActive { get; init; }
    public List<TurnRecord> Turns { get; init; } = [];
}

public record TurnRecord
{
    public string Id { get; init; } = "";
    public int TurnIndex { get; init; }
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public string? ModelId { get; init; }
    public int TokensIn { get; init; }
    public int TokensOut { get; init; }
    public int DurationMs { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
