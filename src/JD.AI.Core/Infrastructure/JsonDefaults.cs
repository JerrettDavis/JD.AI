// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace JD.AI.Core.Infrastructure;

/// <summary>
/// Canonical JSON serialization options. Use these instead of creating
/// new <see cref="JsonSerializerOptions"/> instances throughout the codebase.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Default options: camelCase, relaxed parsing, indented output.
    /// Suitable for tool output, user-facing JSON, and general use.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Compact options: camelCase, no indentation. Suitable for wire formats,
    /// storage, and contexts where size matters.
    /// </summary>
    public static JsonSerializerOptions Compact { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Strict options: PascalCase, no relaxation. Suitable for API contracts
    /// and formal serialization.
    /// </summary>
    public static JsonSerializerOptions Strict { get; } = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Serialize an object using default options.</summary>
    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);

    /// <summary>Serialize an object using compact options.</summary>
    public static string SerializeCompact<T>(T value) =>
        JsonSerializer.Serialize(value, Compact);

    /// <summary>Deserialize JSON using default options.</summary>
    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options);

    /// <summary>Deserialize a JSON element using default options.</summary>
    public static T? Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(Options);
}
