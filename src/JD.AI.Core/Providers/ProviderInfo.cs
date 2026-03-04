namespace JD.AI.Core.Providers;

/// <summary>
/// Describes a detected AI provider and its available models.
/// </summary>
public sealed record ProviderInfo(
    string Name,
    bool IsAvailable,
    string? StatusMessage,
    IReadOnlyList<ProviderModelInfo> Models);

/// <summary>
/// A model available through a provider.
/// </summary>
public sealed record ProviderModelInfo(
    string Id,
    string DisplayName,
    string ProviderName,
    int ContextWindowTokens = 128_000,
    int MaxOutputTokens = 16_384,
    decimal InputCostPerToken = 0m,
    decimal OutputCostPerToken = 0m,
    bool HasMetadata = false);
