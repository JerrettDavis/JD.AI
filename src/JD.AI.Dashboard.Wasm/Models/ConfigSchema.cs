using System.Text.Json.Serialization;

namespace JD.AI.Dashboard.Wasm.Models;

public sealed class ConfigSchema
{
    [JsonPropertyName("sections")]
    public IList<ConfigSection> Sections { get; set; } = [];
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
    public IList<ConfigField> Fields { get; set; } = [];
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
    public IList<string>? EnumValues { get; set; }

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("sensitive")]
    public bool Sensitive { get; set; }
}
