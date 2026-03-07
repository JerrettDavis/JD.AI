using System.Text.Json.Serialization;
using JD.AI.Core.Providers;

namespace JD.AI.Core.LocalModels;

/// <summary>
/// GPU backend type detected or configured for inference.
/// </summary>
public enum GpuBackend
{
    Cpu,
    Cuda,
    Vulkan,
    Metal,
}

/// <summary>
/// GGUF quantization type parsed from the filename.
/// </summary>
public enum QuantizationType
{
    Unknown,
    F32,
    F16,
    Q8_0,
    Q6_K,
    Q5_K_M,
    Q5_K_S,
    Q5_0,
    Q4_K_M,
    Q4_K_S,
    Q4_0,
    Q3_K_M,
    Q3_K_S,
    Q3_K_L,
    Q2_K,
    IQ4_XS,
    IQ3_XXS,
    IQ2_XXS,
}

/// <summary>
/// Where a model was sourced from.
/// </summary>
public enum ModelSourceKind
{
    LocalFile,
    DirectoryScan,
    HuggingFace,
    RemoteUrl,
}

/// <summary>
/// Metadata describing a local GGUF model.
/// </summary>
public sealed record ModelMetadata
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; init; }

    [JsonPropertyName("quantization")]
    public QuantizationType Quantization { get; init; } = QuantizationType.Unknown;

    [JsonPropertyName("parameterSize")]
    public string? ParameterSize { get; init; }

    [JsonPropertyName("source")]
    public ModelSourceKind Source { get; init; } = ModelSourceKind.LocalFile;

    [JsonPropertyName("sourceUri")]
    public string? SourceUri { get; init; }

    [JsonPropertyName("addedUtc")]
    public DateTime AddedUtc { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("fileHash")]
    public string? FileHash { get; init; }

    /// <summary>
    /// Capabilities detected or configured for this model (e.g., Chat, ToolCalling, Vision).
    /// </summary>
    [JsonPropertyName("capabilities")]
    public ModelCapabilities Capabilities { get; init; } = ModelCapabilities.Chat;

    /// <summary>
    /// Parses quantization and parameter size from a GGUF filename.
    /// Example: "Meta-Llama-3-8B-Instruct-Q4_K_M.gguf" → Q4_K_M, "8B"
    /// </summary>
    public static (QuantizationType Quant, string? ParamSize) ParseFilename(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename).ToUpperInvariant();

        var quant = QuantizationType.Unknown;
        foreach (var q in Enum.GetValues<QuantizationType>())
        {
            if (q == QuantizationType.Unknown) continue;
            var qName = q.ToString().ToUpperInvariant().Replace('_', '_');
            if (name.Contains(qName, StringComparison.Ordinal))
            {
                quant = q;
                break;
            }
        }

        string? paramSize = null;
        var paramMatch = System.Text.RegularExpressions.Regex.Match(
            name, @"(\d+\.?\d*)[BM](?=[-_\.\s]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (paramMatch.Success)
        {
            paramSize = paramMatch.Value;
        }

        return (quant, paramSize);
    }

    /// <summary>
    /// Creates a display-friendly name from a GGUF filename.
    /// </summary>
    public static string DisplayNameFromFilename(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        // Replace common separators with spaces for readability
        return name.Replace('-', ' ').Replace('_', ' ');
    }
}

/// <summary>
/// JSON-serializable model registry manifest.
/// </summary>
public sealed record ModelRegistry
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("models")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1002:Do not expose generic lists", Justification = "Internal mutable collection for JSON serialization")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Needs List for mutation")]
    public List<ModelMetadata> Models { get; init; } = [];
}
