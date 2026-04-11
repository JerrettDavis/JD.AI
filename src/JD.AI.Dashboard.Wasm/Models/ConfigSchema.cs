using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

public sealed class ConfigSchema
{
    [JsonPropertyName("sections")]
    public List<ConfigSection> Sections { get; set; } = [];
}

public sealed class ConfigSection
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<ConfigField> Fields { get; set; } = [];
}

public sealed class ConfigField
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("enumValues")]
    public List<string>? EnumValues { get; set; }

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("sensitive")]
    public bool Sensitive { get; set; }
}
