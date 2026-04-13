using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentStatus { Active, Inactive, Error }

public record AgentInfo(string Id, string Provider, string Model, int TurnCount, DateTimeOffset CreatedAt)
{
    [JsonPropertyName("status")]
    public AgentStatus Status { get; init; } = AgentStatus.Active;
};

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

public record AgentDetailInfo
{
    public string Id { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Model { get; init; } = "";
    public string SystemPrompt { get; init; } = "";
    public bool IsDefault { get; init; }
    public ToolInfo[] Tools { get; init; } = [];
    public SkillInfo[] AssignedSkills { get; init; } = [];
}

public record ToolInfo
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsAllowed { get; init; } = true;
}

public record SkillInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public SkillStatus Status { get; init; } = SkillStatus.Ready;
    public bool Enabled { get; init; }
    public string? StatusReason { get; init; }
    public Dictionary<string, string> Config { get; init; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SkillStatus { Ready, NeedsSetup, Disabled }
