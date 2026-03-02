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
    public ModelParameters Parameters { get; set; } = new();
}

/// <summary>Tunable model inference parameters (Ollama, OpenAI, etc.).</summary>
public record ModelParameters
{
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public int? MaxTokens { get; set; }
    public int? ContextWindowSize { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
    public double? RepeatPenalty { get; set; }
    public int? Seed { get; set; }
    public string[] StopSequences { get; set; } = [];
}
