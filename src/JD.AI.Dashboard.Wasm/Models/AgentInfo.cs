namespace JD.AI.Dashboard.Wasm.Models;

public record AgentInfo(string Id, string Provider, string Model, int TurnCount, DateTimeOffset CreatedAt);

public record AgentDefinition
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public bool AutoSpawn { get; set; }
    public int MaxTurns { get; set; }
    public string[] Tools { get; set; } = [];
}
