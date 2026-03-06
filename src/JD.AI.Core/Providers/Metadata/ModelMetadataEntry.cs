namespace JD.AI.Core.Providers.Metadata;

/// <summary>
/// Parsed entry from the LiteLLM model catalog.
/// </summary>
public sealed record ModelMetadataEntry
{
    public required string Key { get; init; }
    public string? LitellmProvider { get; init; }
    public string? Mode { get; init; }
    public int? MaxInputTokens { get; init; }
    public int? MaxOutputTokens { get; init; }
    public decimal? InputCostPerToken { get; init; }
    public decimal? OutputCostPerToken { get; init; }
    public bool? SupportsVision { get; init; }
    public bool? SupportsFunctionCalling { get; init; }
    public bool? SupportsReasoning { get; init; }
}
